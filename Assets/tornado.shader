HEADER
{
	Description = "Soulvivor Rankine Vortex Tornado with Centrifugal Debris";
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
	#include "common/shared.hlsl"

	// ── Color Settings ────────────────────────────────────────────────────────
	float3 g_vInnerVortexColor     < UiGroup( "Color Settings,10/1" ); UiType( Color );  Default3( 0.65, 0.92, 0.98 ); >;
	float3 g_vOuterVortexColor     < UiGroup( "Color Settings,10/2" ); UiType( Color );  Default3( 0.20, 0.45, 0.65 ); >;
	float3 g_vDustDebrisColor      < UiGroup( "Color Settings,10/3" ); UiType( Color );  Default3( 0.85, 0.95, 1.00 ); >;
	float3 g_vCoreEyeColor         < UiGroup( "Color Settings,10/4" ); UiType( Color );  Default3( 0.10, 0.15, 0.25 ); >;

	// ── Vortex Physics ────────────────────────────────────────────────────────
	float  g_flVortexSpeed         < UiGroup( "Vortex Physics,15/1" ); UiType( Slider ); Range( 0.5, 8.0 );  Default( 3.2 ); >;
	float  g_flFunnelWidth         < UiGroup( "Vortex Physics,15/2" ); UiType( Slider ); Range( 0.3, 2.5 );  Default( 1.25 ); >;
	float  g_flFunnelTwist         < UiGroup( "Vortex Physics,15/3" ); UiType( Slider ); Range( 1.0, 10.0 ); Default( 5.5 ); >;
	float  g_flEyeHollowness       < UiGroup( "Vortex Physics,15/4" ); UiType( Slider ); Range( 0.05, 0.7 ); Default( 0.22 ); >;
	float  g_flSuctionLift         < UiGroup( "Vortex Physics,15/5" ); UiType( Slider ); Range( 0.5, 5.0 );  Default( 2.8 ); >;
	float  g_flVortexShear         < UiGroup( "Vortex Physics,15/6" ); UiType( Slider ); Range( 0.2, 3.0 );  Default( 1.6 ); >;

	// ── Debris & Gusts ────────────────────────────────────────────────────────
	float  g_flDebrisDensity       < UiGroup( "Debris & Gusts,20/1" ); UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.8 ); >;
	float  g_flWindFrayStrength    < UiGroup( "Debris & Gusts,20/2" ); UiType( Slider ); Range( 0.0, 2.5 );  Default( 1.4 ); >;
	float  g_flAirDistortion       < UiGroup( "Debris & Gusts,20/3" ); UiType( Slider ); Range( 0.0, 2.0 );  Default( 0.85 ); >;

	float3 g_vMotionVelocity       < UiGroup( "Motion,25/1" ); Default3( 0.0, 0.0, 0.0 ); >;

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

	float2 RotateUV( float2 uv, float angle )
	{
		float s = sin( angle );
		float c = cos( angle );
		return float2( uv.x * c - uv.y * s, uv.x * s + uv.y * c );
	}

	// Safe unrolled FBM using standard vector rotations
	float FbmNoise( float2 p )
	{
		float f = 0.0;
		f += 0.5000 * SimplexNoise2D( p ); p = RotateUV( p, 0.64 ) * 2.02;
		f += 0.2500 * SimplexNoise2D( p ); p = RotateUV( p, 0.64 ) * 2.03;
		f += 0.1250 * SimplexNoise2D( p ); p = RotateUV( p, 0.64 ) * 2.01;
		f += 0.0625 * SimplexNoise2D( p );
		return f / 0.9375;
	}

	float AnisotropicFbm( float2 p, float stretchY )
	{
		p.y *= stretchY;
		return FbmNoise( p );
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
		float progress = saturate( 1.0 - i.vTexCoord.y );

		// Geometric funnel flare (modifies local vertex position)
		float flare = 0.35 + progress * ( g_flFunnelWidth * 0.9 );
		i.vPositionOs.xy *= flare;

		// Suction updraft vertical wave
		float updraftWave = sin( g_flTime * g_flSuctionLift * 3.0 + progress * 8.0 );
		i.vPositionOs.z += updraftWave * 0.08 * progress;

		// Velocity drag
		i.vPositionOs.xy -= clamp( g_vMotionVelocity.xy, -20.0, 20.0 ) * ( progress * 0.3 );

		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"

	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, ONE ); // Additive combustion/vortex glow
	RenderState( DepthWriteEnable, true );
	RenderState( CullMode, NONE );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = i.vTextureCoords.xy;
		float progress = uv.y;
		float centeredX = uv.x - 0.5;
		float radius = abs( centeredX ) * 2.0;

		// 1. Rankine Vortex swirl
		float rotationAngle = ( g_flTime * g_flVortexSpeed * 3.0 ) + ( progress * g_flFunnelTwist * 2.0 );
		float shear = ( 1.0 / max( 0.08, radius + g_flEyeHollowness ) ) * g_flVortexShear * 0.22;
		float swirlingU = uv.x + sin( rotationAngle + shear ) * 0.25;
		float swirlingV = uv.y - ( g_flTime * g_flSuctionLift * 0.5 );
		float2 vortexCoords = float2( swirlingU * 4.0, swirlingV * 2.5 );

		// 2. Turbulent wind fibers
		float2 warp = float2(
			FbmNoise( vortexCoords + float2( 0.0, g_flTime * 0.8 ) ),
			FbmNoise( vortexCoords + float2( 3.7, -g_flTime * 0.6 ) )
		) * 2.0 - 1.0;

		float windFiberA = AnisotropicFbm( vortexCoords + warp * g_flAirDistortion, 4.5 );
		float windFiberB = SimplexNoise2D( vortexCoords * float2( 2.0, 6.0 ) + float2( g_flTime * 2.0, 0.0 ) );
		float windTurbulence = saturate( windFiberA * 0.7 + windFiberB * 0.3 );

		// 3. Condensation wall & hollow eye
		float eyeWallMin = g_flEyeHollowness * ( 0.6 + progress * 0.4 );
		float eyeWallMax = eyeWallMin + max( 0.05, 0.35 * g_flFunnelWidth );

		float condensationWall = smoothstep( eyeWallMin, eyeWallMin + 0.12, radius ) *
		                         ( 1.0 - smoothstep( eyeWallMax, eyeWallMax + 0.3, radius ) );

		float edgeWisps = SimplexNoise2D( float2( centeredX * 8.0, uv.y * 3.0 - g_flTime * 2.0 ) ) * g_flWindFrayStrength * 0.4;
		condensationWall = saturate( condensationWall + edgeWisps * ( 1.0 - smoothstep( eyeWallMax, max( eyeWallMax + 0.05, 1.0 ), radius ) ) );

		float tornadoDensity = condensationWall * ( windTurbulence * 0.8 + 0.4 );

		// 4. Centrifugal debris
		float2 debrisGrid = float2( uv.x * 16.0 + g_flTime * g_flVortexSpeed * 2.0, ( uv.y - g_flTime * g_flSuctionLift * 1.5 ) * 20.0 );
		float2 debrisRand = Hash22( floor( debrisGrid ) );
		float debrisDist = length( frac( debrisGrid ) - 0.5 - debrisRand * 0.25 );
		float debrisMask = 1.0 - smoothstep( 0.05, 0.3, debrisDist );

		float debrisEdge0 = min( 0.75, eyeWallMin * 0.85 );
		float debrisEdge1 = max( debrisEdge0 + 0.15, 0.95 );
		float flyingDebris = debrisMask * g_flDebrisDensity * smoothstep( debrisEdge0, debrisEdge1, radius );

		// 5. Fresnel rim
		float3 viewDir = normalize( CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz ) );
		float3 normal = normalize( i.vNormalWs.xyz );
		float NdotV = saturate( abs( dot( normal, viewDir ) ) );
		float rimRefract = pow( 1.0 - NdotV, 2.2 );

		// 6. Color
		float3 airColor = lerp( g_vCoreEyeColor, g_vOuterVortexColor, smoothstep( 0.0, eyeWallMin + 0.1, radius ) );
		airColor = lerp( airColor, g_vInnerVortexColor, saturate( condensationWall * windTurbulence * 1.5 ) );
		airColor += rimRefract * g_vInnerVortexColor * 0.8;
		airColor = lerp( airColor, g_vDustDebrisColor, flyingDebris * 0.9 );

		float alpha = saturate( tornadoDensity * 1.3 + flyingDebris * 0.9 + rimRefract * 0.3 );

		return float4( airColor, alpha );
	}
}