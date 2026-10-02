HEADER
{
	Description = "Soulvivor Volumetric Fire Plume with Frayed Tendrils & Aerodynamic Motion";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
}

COMMON
{	
	#ifndef S_TRANSLUCENT
        #define S_TRANSLUCENT 1
    #endif

    #ifndef S_ALPHA_TEST
        #define S_ALPHA_TEST 0
    #endif
	#include "common/shared.hlsl"
	

	// ── Color Settings ────────────────────────────────────────────────────────
	float3 g_vInnerFlameColor     < UiGroup( "Color Settings,10/1" ); UiType( Color );  Default3( 0.15, 0.85, 0.95 ); >;
	float3 g_vOuterFlameColor     < UiGroup( "Color Settings,10/2" ); UiType( Color );  Default3( 0.25, 0.05, 0.75 ); >;
	float3 g_vCombustionEdgeColor < UiGroup( "Color Settings,10/3" ); UiType( Color );  Default3( 0.55, 0.95, 1.00 ); >;
	float3 g_vWhiteHotCoreColor   < UiGroup( "Color Settings,10/4" ); UiType( Color );  Default3( 0.90, 0.98, 1.00 ); >;

	// ── Flame Substance ───────────────────────────────────────────────────────
	float  g_flCoreDensity         < UiGroup( "Flame Substance,15/1" ); UiType( Slider ); Range( 0.0, 3.0 );   Default( 1.5 ); >;
	float  g_flCoreWidth           < UiGroup( "Flame Substance,15/2" ); UiType( Slider ); Range( 0.1, 2.0 );   Default( 1.0 ); >;
	float  g_flSubstanceContrast   < UiGroup( "Flame Substance,15/3" ); UiType( Slider ); Range( 0.5, 4.0 );   Default( 1.8 ); >;

	// ── Frayed Tendrils ───────────────────────────────────────────────────────
	float  g_flFrayStrength        < UiGroup( "Frayed Tendrils,20/1" ); UiType( Slider ); Range( 0.0, 3.0 );   Default( 1.2 ); >;
	float  g_flAnisotropicStretch  < UiGroup( "Frayed Tendrils,20/2" ); UiType( Slider ); Range( 1.0, 8.0 );   Default( 3.5 ); >;
	float  g_flTongueSeparation    < UiGroup( "Frayed Tendrils,20/3" ); UiType( Slider ); Range( 0.0, 3.0 );   Default( 1.2 ); >;

	// ── Motion Trail ──────────────────────────────────────────────────────────
	float  g_flTrailLength         < UiGroup( "Motion Trail,25/1" ); UiType( Slider ); Range( 0.1, 4.0 );   Default( 1.8 ); >;
	float  g_flTrailPinch          < UiGroup( "Motion Trail,25/2" ); UiType( Slider ); Range( 0.1, 2.5 );   Default( 1.0 ); >;
	float  g_flWakeTurbulence      < UiGroup( "Motion Trail,25/3" ); UiType( Slider ); Range( 0.0, 2.0 );   Default( 0.8 ); >;
	float  g_flEmberDensity        < UiGroup( "Motion Trail,25/4" ); UiType( Slider ); Range( 0.0, 3.0 );   Default( 1.2 ); >;

	// ── Base Dynamics ─────────────────────────────────────────────────────────
	float  g_flTurbulenceSpeed     < UiGroup( "Base Dynamics,30/1" ); UiType( Slider ); Range( 0.5, 8.0 );   Default( 2.5 ); >;
	float  g_flEmissionOverdrive   < UiGroup( "Base Dynamics,30/2" ); UiType( Slider ); Range( 0.1, 4.0 );   Default( 1.5 ); >;
	float  g_flDissolveAmount      < UiGroup( "Base Dynamics,30/3" ); UiType( Slider ); Range( 0.0, 1.0 );   Default( 0.0 ); >;
	float  g_flFlameLength         < UiGroup( "Base Dynamics,30/4" ); UiType( Slider ); Range( 0.2, 10.0 );  Default( 1.5 ); >;
	float  g_flThermalBuoyancy     < UiGroup( "Base Dynamics,30/5" ); UiType( Slider ); Range( 0.0, 2.0 );   Default( 0.5 ); >;
	float  g_flWarpStrength        < UiGroup( "Base Dynamics,30/6" ); UiType( Slider ); Range( 0.0, 5.0 );   Default( 1.0 ); >;

	float3 g_vMotionVelocity       < UiGroup( "Motion,40/1" ); Default3( 0.0, 0.0, 0.0 ); >;

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
		float3 n = h * h * h * h * float3(
			dot( a, Hash22( i ) ),
			dot( b, Hash22( i + o ) ),
			dot( c, Hash22( i + 1.0 ) )
		);

		return dot( n, float3( 70.0, 70.0, 70.0 ) ) * 0.5 + 0.5;
	}

	float FbmNoise( float2 p )
	{
		float f = 0.0;
		f += 0.5000 * SimplexNoise2D( p ); p = mul( float2x2( 0.80, 0.60, -0.60, 0.80 ), p ) * 2.02;
		f += 0.2500 * SimplexNoise2D( p ); p = mul( float2x2( 0.80, 0.60, -0.60, 0.80 ), p ) * 2.03;
		f += 0.1250 * SimplexNoise2D( p );
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
		// Gentle thermal displacement safely modulated along vertex normal (like fire_inferno_plume)
		float wave = sin( g_flTime * ( g_flTurbulenceSpeed * 2.0 ) + i.vPositionOs.z * 0.1 );
		float thermalLift = max( 0.0, wave ) * ( g_flThermalBuoyancy * 2.0 );

		i.vPositionOs.z += thermalLift;
		i.vPositionOs.xy += i.vNormalOs.xy * ( wave * 0.3 * g_flThermalBuoyancy );

		// Aerodynamic drag lag from velocity
		float dragFactor = saturate( 1.0 - i.vTexCoord.y ) * 0.2;
		i.vPositionOs.xy -= clamp( g_vMotionVelocity.xy, -20.0, 20.0 ) * dragFactor;

		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	
	RenderState( CullMode, NONE ); // Double sided

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = i.vTextureCoords.xy;

		// 1. Upward Flow & Domain Warping
		float flowRate = g_flTime * g_flTurbulenceSpeed * 0.6;
		float2 flowUV = float2( uv.x, uv.y * 1.5 - flowRate );

		float2 warp = float2(
			FbmNoise( flowUV * 0.8 + float2( 0.0, flowRate * 0.25 ) ),
			FbmNoise( flowUV * 0.8 + float2( 3.7, flowRate * 0.25 ) )
		) * 2.0 - 1.0;

		// 2. Karman Vortex lateral wake oscillation
		float karmanWake = sin( uv.y * 10.0 - flowRate * 3.0 ) * ( 0.12 * g_flWakeTurbulence );
		float2 frayedUV = flowUV + warp * ( g_flWarpStrength * 0.3 );
		frayedUV.x += karmanWake * saturate( 1.0 - uv.y );

		// 3. Anisotropic Tendrils & Tongues
		float2 stretchedUV = float2( frayedUV.x, frayedUV.y * ( 1.0 / max( 0.1, g_flAnisotropicStretch ) ) );
		float noiseA = FbmNoise( stretchedUV * 1.8 );
		float noiseB = SimplexNoise2D( stretchedUV * 3.5 + float2( flowRate * 0.5, 0.0 ) );
		float tendrilNoise = saturate( noiseA * 0.65 + noiseB * 0.35 );

		// Tendril Tongue Splitting
		float tongueSplit = abs( sin( ( uv.x - 0.5 ) * 12.0 * g_flTongueSeparation + tendrilNoise * 3.14 ) );
		tendrilNoise = lerp( tendrilNoise, tendrilNoise * tongueSplit, saturate( g_flFrayStrength * 0.4 ) );

		// 4. Solid Combustion Core & Vertical Decay
		float verticalDecay = saturate( 1.0 - ( uv.y / max( 0.2, g_flFlameLength ) ) );
		float analyticalCore = pow( verticalDecay, 1.2 ) * ( g_flCoreDensity * 0.6 );

		// Modulate with Dissolve Amount
		float flameIntensity = saturate( ( ( tendrilNoise * verticalDecay + analyticalCore ) * g_flSubstanceContrast ) - g_flDissolveAmount );
		float flameMask = smoothstep( 0.05, 0.35, flameIntensity );

		// 5. Detached Embers & Spark Trail
		float2 emberGrid = float2( uv.x * 20.0, ( uv.y - flowRate * 1.5 ) * 25.0 );
		float2 emberRand = Hash22( floor( emberGrid ) );
		float emberDist = length( frac( emberGrid ) - 0.5 - emberRand * 0.25 );
		float emberMask = smoothstep( 0.2, 0.02, emberDist );
		float emberFlicker = sin( g_flTime * 25.0 + emberRand.x * 30.0 ) * 0.5 + 0.5;
		float sparks = emberMask * emberFlicker * g_flEmberDensity * saturate( 1.0 - ( uv.y / max( 0.1, g_flTrailLength ) ) );

		// 6. Color Gradient & Blackbody Emission
		float3 baseFlame = lerp( g_vOuterFlameColor, g_vInnerFlameColor, smoothstep( 0.15, 0.65, flameIntensity ) );
		float3 coreIncandescence = lerp( baseFlame, g_vWhiteHotCoreColor, smoothstep( 0.65, 1.2, flameIntensity + analyticalCore * 0.5 ) );
		float3 finalEmission = coreIncandescence * g_flEmissionOverdrive;

		// Combustion Edge Contour
		float edgeContour = smoothstep( 0.02, 0.15, flameIntensity ) * ( 1.0 - smoothstep( 0.15, 0.45, flameIntensity ) );
		finalEmission += g_vCombustionEdgeColor * edgeContour * 2.0;

		// Sparks color
		finalEmission += g_vCombustionEdgeColor * sparks * 3.0;

		// Finale Transparenzberechnung
		float alpha = saturate( flameMask * 1.4 + sparks );
		
		return float4( finalEmission, alpha );
	}
}