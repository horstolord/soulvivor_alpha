HEADER
{
	Description = "Soulvivor Gaseous Fire Convection & Turbulent Flame Plume";
}

FEATURES
{
	#include "common/features.hlsl"
	Feature( F_TRANSLUCENT, 0..1, "Rendering" );
}

MODES
{
	VrForward();
	Depth();
	ToolsVis(True);
}

COMMON
{
	#define S_TRANSLUCENT 1
	#include "common/shared.hlsl"

	// Colors
	float3 InnerFlameColor     < UiGroup( "Color Settings, 10/1" ); UiType( Color );  Default3( 1.0, 0.7, 0.1 ); >;
	float3 OuterFlameColor     < UiGroup( "Color Settings, 10/2" ); UiType( Color );  Default3( 1.0, 0.15, 0.0 ); >;
	float3 CombustionEdgeColor < UiGroup( "Color Settings, 10/3" ); UiType( Color );  Default3( 1.0, 0.95, 0.6 ); >;

	// Flame Dynamics
	float  TurbulenceSpeed     < UiGroup( "Flame Dynamics, 20/1" ); UiType( Slider ); Range( 0.5, 8.0 );   Default( 2.5 ); >;
	float  EmissionOverdrive   < UiGroup( "Flame Dynamics, 20/2" ); UiType( Slider ); Range( 0.1, 3.0 );   Default( 1.0 ); >;
	float  DissolveAmount      < UiGroup( "Flame Dynamics, 20/3" ); UiType( Slider ); Range( 0.0, 1.0 );   Default( 0.0 ); >;
	float  FlameLength         < UiGroup( "Flame Dynamics, 20/4" ); UiType( Slider ); Range( 0.1, 10.0 );  Default( 1.0 ); >;
	float  ThermalBuoyancy     < UiGroup( "Flame Dynamics, 20/5" ); UiType( Slider ); Range( 0.0, 2.0 );   Default( 0.6 ); >;
	float  WarpStrength        < UiGroup( "Flame Dynamics, 20/6" ); UiType( Slider ); Range( 0.0, 5.0 );   Default( 0.75 ); >;

	// Procedural Noise Functions
	float2 Hash22( float2 p )
	{
		float3 p3 = frac( float3( p.xyx ) * float3( 0.1031, 0.1030, 0.0973 ) );
		p3 += dot( p3, p3.yzx + 33.33 );
		return frac( ( p3.xx + p3.yz ) * p3.zy ) * 2.0 - 1.0;
	}

	float SimplexNoise2D( float2 p )
	{
		const float K1 = 0.366025404;
		const float K2 = 0.211324865;

		float2 i = floor( p + ( p.x + p.y ) * K1 );
		float2 a = p - i + ( i.x + i.y ) * K2;
		float2 o = ( a.x > a.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
		float2 b = a - o + K2;
		float2 c = a - 1.0 + 2.0 * K2;

		float3 h = max( 0.5 - float3( dot( a, a ), dot( b, b ), dot( c, c ) ), 0.0 );
		float3 n = h * h * h * h * float3( dot( a, Hash22( i ) ), dot( b, Hash22( i + o ) ), dot( c, Hash22( i + 1.0 ) ) );

		return dot( n, float3( 70.0, 70.0, 70.0 ) ) * 0.5 + 0.5;
	}

	float FbmNoise( float2 uv )
	{
		float f = 0.0;
		f += 0.5000 * SimplexNoise2D( uv ); uv *= 2.02;
		f += 0.2500 * SimplexNoise2D( uv ); uv *= 2.03;
		f += 0.1250 * SimplexNoise2D( uv );
		return f / 0.875;
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
		// Vertex thermal displacement on object-space coordinates
		float wave = sin( g_flTime * ( TurbulenceSpeed * 2.0 ) + i.vPositionOs.z * 0.1 );
		float thermalLift = max( 0.0, wave ) * ( ThermalBuoyancy * 4.0 );

		i.vPositionOs.z += thermalLift;
		i.vPositionOs.xy += i.vNormalOs.xy * ( wave * 0.5 * ThermalBuoyancy );

		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	RenderState( BlendEnable, true );

	// 1. Premultiplied Linear Blend:
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );

	// 2. Explicit Reversed-Z Depth Testing:
	RenderState( DepthEnable, true)
	RenderState( DepthWriteEnable, false );
	RenderState( DepthFunc, GREATER_EQUAL );

	RenderState( CullMode, BACK );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		// Upward UV convection
		float flowRate = g_flTime * TurbulenceSpeed * 0.5;
		float2 uvFlow = i.vTextureCoords.xy * float2( 1.0, 2.0 ) - float2( 0.0, flowRate );

		float2 warp = float2(
			FbmNoise( uvFlow * 0.8 + float2( 0.0, flowRate * 0.3 ) ),
			FbmNoise( uvFlow * 0.8 + float2( 5.2, flowRate * 0.3 ) )
		) * 2.0 - 1.0;

		// Dual octave turbulent gas sampling
		float noiseA = FbmNoise( uvFlow * float2( 2.0, 0.9 ) + warp * WarpStrength );
		float noiseB = FbmNoise( uvFlow * 1.8 + float2( flowRate * 0.2, 0.0 ) );
		float turbulentGas = saturate( noiseA * 0.6 + noiseB * 0.4 );

		// Camera view angle & Rim Fresnel
		float3 viewDir = normalize( CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs ) );
		float3 normal = normalize( i.vNormalWs );
		float NdotV = saturate( abs( dot( normal, viewDir ) ) );
		float rimGlow = pow( 1.0 - NdotV, 2.5 );

		// --- UPWARD DIRECTIONAL DISSOLVE ---
		// Base (V = 1.0) is solid -> dissolves towards the top (V = 0.0)
		float upwardHeight = saturate( 1.0 - i.vTextureCoords.y / max( 0.01, FlameLength ) );
		float flameDensity = 1.0 - pow( upwardHeight, 1.5 );

		// Modulate noise with height density and user dissolve slider
		float fireIntensity = saturate( ( turbulentGas * flameDensity ) - DissolveAmount );

		// Mask for the flame body
		float dissolveMask = smoothstep( 0.04, 0.30, fireIntensity );

		// Burning combustion edge where flame is tearing
		float combustionEdge = smoothstep( 0.01, 0.12, fireIntensity ) *
		                       ( 1.0 - smoothstep( 0.12, 0.40, fireIntensity ) );

		// Core Incandescent Gradient + Rim Glow
		float3 fireColor = lerp( OuterFlameColor, InnerFlameColor, pow( saturate( fireIntensity * 1.5 ), 1.6 ) );
		float3 finalEmission = ( fireColor + rimGlow * OuterFlameColor ) + ( combustionEdge * CombustionEdgeColor * 3.0 );

		finalEmission *= EmissionOverdrive;
		float alpha = saturate( dissolveMask * 1.5 );

		return float4( finalEmission, alpha );
	}
}