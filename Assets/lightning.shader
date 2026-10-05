HEADER
{
	Description = "Soulvivor Crackling Arc Lightning with Piecewise Stepped Leaders & Branch Bifurcation";
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

	// ── Electrical Palette ────────────────────────────────────────────────────
	float3 g_vCoreColor           < UiGroup( "Color Settings,10/1" ); UiType( Color );  Default3( 1.00, 1.00, 1.00 ); >;
	float3 g_vCoronaGlowColor     < UiGroup( "Color Settings,10/2" ); UiType( Color );  Default3( 0.20, 0.75, 1.00 ); >;
	float3 g_vBranchForkColor     < UiGroup( "Color Settings,10/3" ); UiType( Color );  Default3( 0.55, 0.30, 0.98 ); >;

	// ── Bolt Geometry & Thickness ─────────────────────────────────────────────
	float  g_flTrunkThickness     < UiGroup( "Bolt Geometry,15/1" );  UiType( Slider ); Range( 0.001, 0.08 ); Default( 0.015 ); >;
	float  g_flForkThickness      < UiGroup( "Bolt Geometry,15/2" );  UiType( Slider ); Range( 0.0005, 0.04 ); Default( 0.006 ); >;
	float  g_flCoronaGlowRadius   < UiGroup( "Bolt Geometry,15/3" );  UiType( Slider ); Range( 0.01, 0.35 );  Default( 0.12 ); >;
	float  g_flSegmentDensity     < UiGroup( "Bolt Geometry,15/4" );  UiType( Slider ); Range( 4.0, 40.0 );   Default( 16.0 ); >;
	float  g_flJaggedWidth        < UiGroup( "Bolt Geometry,15/5" );  UiType( Slider ); Range( 0.05, 0.45 );  Default( 0.22 ); >;

	// ── Branching & Forking ───────────────────────────────────────────────────
	float  g_flBranchIntensity    < UiGroup( "Branching,20/1" );      UiType( Slider ); Range( 0.0, 2.0 );   Default( 1.2 ); >;
	float  g_flForkSpread         < UiGroup( "Branching,20/2" );      UiType( Slider ); Range( 0.05, 0.6 );  Default( 0.28 ); >;
	float  g_flForkStartPoint     < UiGroup( "Branching,20/3" );      UiType( Slider ); Range( 0.1, 0.8 );   Default( 0.25 ); >;

	// ── Temporal Dynamics ─────────────────────────────────────────────────────
	float  g_flCrackleSpeed       < UiGroup( "Temporal Dynamics,25/1" ); UiType( Slider ); Range( 5.0, 60.0 );  Default( 24.0 ); >;
	float  g_flStrobeFlicker      < UiGroup( "Temporal Dynamics,25/2" ); UiType( Slider ); Range( 0.0, 1.0 );   Default( 0.85 ); >;
	float  g_flEmissionOverdrive  < UiGroup( "Temporal Dynamics,25/3" ); UiType( Slider ); Range( 1.0, 10.0 );  Default( 4.5 ); >;

	float3 g_vMotionVelocity      < UiGroup( "Motion,30/1" ); Default3( 0.0, 0.0, 0.0 ); >;

	// High-performance hash
	float2 Hash22( float2 p )
	{
		float3 p3 = frac( float3( p.xyx ) * float3( 0.1031, 0.1030, 0.0973 ) );
		p3 += dot( p3, p3.yzx + 33.33 );
		return frac( ( p3.xx + p3.yz ) * p3.zy ) * 2.0 - 1.0;
	}

	// Piecewise-Linear Stepped Leader Node Generator
	float GetAngularBoltOffset( float y, float frequency, float seed )
	{
		float seg = y * frequency;
		float i0 = floor( seg );
		float i1 = i0 + 1.0;
		float f = frac( seg );

		// Sharp linear interpolation between discrete node coordinates
		float node0 = Hash22( float2( i0, seed ) ).x;
		float node1 = Hash22( float2( i1, seed ) ).x;
		float linearPath = lerp( node0, node1, f );

		// Micro high-frequency sub-fracture
		float microSeg = y * frequency * 3.0;
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
		// High-voltage dielectric jitter displacement
		float steppedTime = floor( g_flTime * g_flCrackleSpeed );
		float jitter = Hash22( float2( steppedTime, i.vPositionOs.z ) ).x * 0.02;
		i.vPositionOs.xy += i.vNormalOs.xy * jitter;

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

		// 1. Stroboscopic Temporal Quantization (Snapping Leader Frames)
		float steppedTime = floor( g_flTime * g_flCrackleSpeed );
		float microFlicker = frac( sin( g_flTime * 120.0 + steppedTime ) * 43758.5453 );
		float strikePower = lerp( 1.0, microFlicker, g_flStrobeFlicker );

		// 2. Primary Trunk Channel Path
		float trunkPath = 0.5 + GetAngularBoltOffset( uv.y, g_flSegmentDensity, steppedTime * 1.37 ) * g_flJaggedWidth;
		float distToTrunk = abs( uv.x - trunkPath );

		// Primary Trunk Arc & Corona Profile
		float trunkCore = saturate( 1.0 - ( distToTrunk / max( 0.0005, g_flTrunkThickness ) ) );
		trunkCore = pow( trunkCore, 6.0 ); // Hyper-sharp white core filament

		float trunkCorona = saturate( 1.0 - ( distToTrunk / max( 0.005, g_flCoronaGlowRadius ) ) );
		trunkCorona = pow( trunkCorona, 3.5 ); // Exponential ionization halo

		// 3. Left Branch Fork
		float leftForkStart = g_flForkStartPoint + Hash22( float2( steppedTime, 3.1 ) ).x * 0.15;
		float leftForkProgress = saturate( ( uv.y - leftForkStart ) / max( 0.05, 1.0 - leftForkStart ) );
		float leftBranchMask = step( leftForkStart, uv.y );

		float leftBranchPath = trunkPath - ( leftForkProgress * g_flForkSpread ) + 
		                       GetAngularBoltOffset( uv.y, g_flSegmentDensity * 1.4, steppedTime + 29.3 ) * ( g_flJaggedWidth * 0.65 );
		float distToLeftBranch = abs( uv.x - leftBranchPath );

		float leftForkCore = saturate( 1.0 - ( distToLeftBranch / max( 0.0005, g_flForkThickness ) ) ) * leftBranchMask * ( 1.0 - leftForkProgress * 0.5 );
		leftForkCore = pow( leftForkCore, 5.0 );

		float leftForkCorona = saturate( 1.0 - ( distToLeftBranch / max( 0.005, g_flCoronaGlowRadius * 0.65 ) ) ) * leftBranchMask;
		leftForkCorona = pow( leftForkCorona, 3.0 );

		// 4. Right Branch Fork (Secondary Bifurcation)
		float rightForkStart = g_flForkStartPoint + 0.18 + Hash22( float2( steppedTime, 9.7 ) ).x * 0.12;
		float rightForkProgress = saturate( ( uv.y - rightForkStart ) / max( 0.05, 1.0 - rightForkStart ) );
		float rightBranchMask = step( rightForkStart, uv.y );

		float rightBranchPath = trunkPath + ( rightForkProgress * g_flForkSpread * 1.1 ) + 
		                        GetAngularBoltOffset( uv.y, g_flSegmentDensity * 1.6, steppedTime + 71.8 ) * ( g_flJaggedWidth * 0.6 );
		float distToRightBranch = abs( uv.x - rightBranchPath );

		float rightForkCore = saturate( 1.0 - ( distToRightBranch / max( 0.0005, g_flForkThickness ) ) ) * rightBranchMask * ( 1.0 - rightForkProgress * 0.5 );
		rightForkCore = pow( rightForkCore, 5.0 );

		float rightForkCorona = saturate( 1.0 - ( distToRightBranch / max( 0.005, g_flCoronaGlowRadius * 0.65 ) ) ) * rightBranchMask;
		rightForkCorona = pow( rightForkCorona, 3.0 );

		// 5. Total Channel Composition
		float totalCore = saturate( trunkCore + ( leftForkCore + rightForkCore ) * g_flBranchIntensity );
		float totalCorona = saturate( trunkCorona + ( leftForkCorona + rightForkCorona ) * g_flBranchIntensity );

		// 6. Spectral Color Assembly
		float3 emissionColor = lerp( g_vCoronaGlowColor, g_vBranchForkColor, saturate( ( leftForkCorona + rightForkCorona ) * 1.5 ) );
		emissionColor = lerp( emissionColor, g_vCoreColor, totalCore );

		float3 finalEmission = emissionColor * ( totalCorona * 1.8 + totalCore * 4.0 ) * ( g_flEmissionOverdrive * strikePower );

		// 7. Clean Negative-Space Opacity
		// (Outside the core and glow aura, alpha evaluates to 0.0 for pure transparency)
		float totalAlpha = saturate( ( totalCore * 1.0 + totalCorona * 0.75 ) * strikePower );

		#if defined( S_TRANSLUCENT ) && S_TRANSLUCENT
			float flSceneDepth = Depth::GetLinear( i.vPositionSs.xy );
			float flDepthFade = saturate( ( flSceneDepth - i.vPositionSs.w ) / 8.0 );
			totalAlpha *= flDepthFade;
		#endif

		// 8. Feed S&box PBR Engine
		m.Albedo = float3( 0.0, 0.0, 0.0 );
		m.Emission = finalEmission;
		m.Opacity = totalAlpha;
		m.Roughness = 1.0;
		m.Metalness = 0.0;

		return ShadingModelStandard::Shade( i, m );
	}
}