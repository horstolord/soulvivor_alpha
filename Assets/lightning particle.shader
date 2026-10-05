HEADER
{
	Description = "Soulvivor Lightning Beam / Ribbon Particle Shader";
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_TRANSLUCENT, 0..1, "Rendering" );
}

MODES
{
	Default();
	Forward();
	Depth();
	ToolsVis( true );
}

COMMON
{
	#define S_TRANSLUCENT 1
	#include "common/shared.hlsl"

	// ── Electrical Colors ─────────────────────────────────────────────────────
	float3 g_vCoreColor           < UiGroup( "Color Settings,10/1" ); UiType( Color );  Default3( 1.00, 1.00, 1.00 ); >;
	float3 g_vCoronaGlowColor     < UiGroup( "Color Settings,10/2" ); UiType( Color );  Default3( 0.20, 0.75, 1.00 ); >;
	float3 g_vBranchForkColor     < UiGroup( "Color Settings,10/3" ); UiType( Color );  Default3( 0.55, 0.30, 0.98 ); >;

	// ── Line Bolt Dynamics ────────────────────────────────────────────────────
	float  g_flTrunkThickness     < UiGroup( "Bolt Parameters,15/1" ); UiType( Slider ); Range( 0.001, 0.15 ); Default( 0.03 ); >;
	float  g_flCoronaGlowRadius   < UiGroup( "Bolt Parameters,15/2" ); UiType( Slider ); Range( 0.01, 0.45 );  Default( 0.22 ); >;
	float  g_flJaggedFrequency    < UiGroup( "Bolt Parameters,15/3" ); UiType( Slider ); Range( 4.0, 50.0 );   Default( 20.0 ); >;
	float  g_flJaggedWidth        < UiGroup( "Bolt Parameters,15/4" ); UiType( Slider ); Range( 0.05, 0.45 );  Default( 0.25 ); >;

	// ── Temporal Crackle ──────────────────────────────────────────────────────
	float  g_flCrackleSpeed       < UiGroup( "Crackle,20/1" );        UiType( Slider ); Range( 5.0, 60.0 );  Default( 28.0 ); >;
	float  g_flStrobeFlicker      < UiGroup( "Crackle,20/2" );        UiType( Slider ); Range( 0.0, 1.0 );   Default( 0.75 ); >;
	float  g_flEmissionOverdrive  < UiGroup( "Crackle,20/3" );        UiType( Slider ); Range( 1.0, 10.0 );  Default( 4.0 ); >;

	float2 Hash22( float2 p )
	{
		float3 p3 = frac( float3( p.xyx ) * float3( 0.1031, 0.1030, 0.0973 ) );
		p3 += dot( p3, p3.yzx + 33.33 );
		return frac( ( p3.xx + p3.yz ) * p3.zy ) * 2.0 - 1.0;
	}

	float GetAngularBoltOffset( float y, float frequency, float seed )
	{
		float seg = y * frequency;
		float i0 = floor( seg );
		float i1 = i0 + 1.0;
		float f = frac( seg );

		float node0 = Hash22( float2( i0, seed ) ).x;
		float node1 = Hash22( float2( i1, seed ) ).x;
		float linearPath = lerp( node0, node1, f );

		float microSeg = y * frequency * 2.5;
		float microNode0 = Hash22( float2( floor( microSeg ), seed + 17.1 ) ).x;
		float microNode1 = Hash22( float2( floor( microSeg ) + 1.0, seed + 17.1 ) ).x;
		float microPath = lerp( microNode0, microNode1, frac( microSeg ) ) * 0.25;

		return linearPath + microPath;
	}
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		// Clean passthrough: lets the particle line emitter calculate world quad positions
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	RenderState( DepthEnable, true );
	RenderState( DepthWriteEnable, false );
	RenderState( CullMode, NONE );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );

		float2 uv = i.vTextureCoords.xy;

		// 1. Stroboscopic Temporal Quantization
		float steppedTime = floor( g_flTime * g_flCrackleSpeed );
		float microFlicker = frac( sin( g_flTime * 120.0 + steppedTime ) * 43758.5453 );
		float strikePower = lerp( 1.0, microFlicker, g_flStrobeFlicker );

		// 2. Bolt Path along the length of the ribbon (uv.y)
		float trunkPath = 0.5 + GetAngularBoltOffset( uv.y, g_flJaggedFrequency, steppedTime * 1.37 ) * g_flJaggedWidth;
		float distToTrunk = abs( uv.x - trunkPath );

		// 3. Core Filament & Corona Glow
		float trunkCore = saturate( 1.0 - ( distToTrunk / max( 0.0005, g_flTrunkThickness ) ) );
		trunkCore = pow( trunkCore, 5.0 );

		float trunkCorona = saturate( 1.0 - ( distToTrunk / max( 0.005, g_flCoronaGlowRadius ) ) );
		trunkCorona = pow( trunkCorona, 3.0 );

		// 4. Color & Emission (Multiplied by particle emitter tint i.vColor)
		float3 baseEmission = lerp( g_vCoronaGlowColor, g_vCoreColor, trunkCore );
		float3 finalEmission = baseEmission * ( trunkCorona * 1.5 + trunkCore * 3.5 ) * g_flEmissionOverdrive * strikePower;

		// Scale emission by particle system color
		finalEmission *= i.vColor.rgb;

		// 5. Opacity scaled by particle lifetime alpha
		float alpha = saturate( trunkCore * 1.0 + trunkCorona * 0.8 ) * strikePower * i.vColor.a;

		// 6. Camera Depth Soft Fade
		#if defined( S_TRANSLUCENT ) && S_TRANSLUCENT
			float flSceneDepth = Depth::GetLinear( i.vPositionSs.xy );
			float flDepthFade = saturate( ( flSceneDepth - i.vPositionSs.w ) / 6.0 );
			alpha *= flDepthFade;
		#endif

		// 7. Output to PBR Engine
		m.Albedo = float3( 0.0, 0.0, 0.0 );
		m.Emission = finalEmission;
		m.Opacity = alpha;
		m.Roughness = 1.0;
		m.Metalness = 0.0;

		return ShadingModelStandard::Shade( i, m );
	}
}