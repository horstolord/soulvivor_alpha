HEADER
{
	Description = "Soulvivor Lofi HD Sakuga Fire with Anti-Aliased Isotherms & Vortex Shedding";
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

	// ── Lofi HD Color Isotherms ───────────────────────────────────────────────
	float3 g_vWhiteHotCoreColor   < UiGroup( "Color Palette,10/1" ); UiType( Color );  Default3( 1.00, 1.00, 0.95 ); >;
	float3 g_vInnerPlasmaColor    < UiGroup( "Color Palette,10/2" ); UiType( Color );  Default3( 0.15, 0.88, 1.00 ); >;
	float3 g_vOuterCombustionColor< UiGroup( "Color Palette,10/3" ); UiType( Color );  Default3( 0.28, 0.08, 0.82 ); >;
	float3 g_vSootSmokeColor      < UiGroup( "Color Palette,10/4" ); UiType( Color );  Default3( 0.08, 0.07, 0.10 ); >;

	// ── Sakuga Isotherm Quantization ──────────────────────────────────────────
	float  g_flIsothermBands       < UiGroup( "Lofi HD Quantization,15/1" ); UiType( Slider ); Range( 2.0, 8.0 );   Default( 4.0 ); >;
	float  g_flCelEdgeCrispness    < UiGroup( "Lofi HD Quantization,15/2" ); UiType( Slider ); Range( 0.5, 5.0 );   Default( 2.0 ); >;
	float  g_flPinchOffFrequency   < UiGroup( "Lofi HD Quantization,15/3" ); UiType( Slider ); Range( 1.0, 10.0 );  Default( 4.5 ); >;
	float  g_flPinchOffIntensity   < UiGroup( "Lofi HD Quantization,15/4" ); UiType( Slider ); Range( 0.0, 2.0 );   Default( 0.85 ); >;

	// ── Vortex Fluid Hydrodynamics ────────────────────────────────────────────
	float  g_flAscentSpeed         < UiGroup( "Vortex Dynamics,20/1" ); UiType( Slider ); Range( 0.5, 8.0 );  Default( 2.8 ); >;
	float  g_flCurlTurbulence      < UiGroup( "Vortex Dynamics,20/2" ); UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.35 ); >;
	float  g_flBuoyancySpread      < UiGroup( "Vortex Dynamics,20/3" ); UiType( Slider ); Range( 0.2, 3.0 );  Default( 1.2 ); >;
	float  g_flFlameTaper          < UiGroup( "Vortex Dynamics,20/4" ); UiType( Slider ); Range( 0.5, 5.0 );  Default( 1.8 ); >;

	// ── High-Velocity Spallation Streaks ──────────────────────────────────────
	float  g_flSparkDensity        < UiGroup( "Sparks & Embers,25/1" ); UiType( Slider ); Range( 0.0, 4.0 );  Default( 1.6 ); >;
	float  g_flSparkElongation     < UiGroup( "Sparks & Embers,25/2" ); UiType( Slider ); Range( 1.0, 12.0 ); Default( 6.0 ); >;
	float  g_flEmissionOverdrive   < UiGroup( "Sparks & Embers,25/3" ); UiType( Slider ); Range( 0.5, 6.0 );  Default( 2.5 ); >;

	float3 g_vMotionVelocity       < UiGroup( "Motion,30/1" ); Default3( 0.0, 0.0, 0.0 ); >;

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

	float FbmNoise( float2 p )
	{
		float f = 0.0;
		f += 0.5000 * SimplexNoise2D( p ); p = mul( float2x2( 0.80, 0.60, -0.60, 0.80 ), p ) * 2.02;
		f += 0.2500 * SimplexNoise2D( p ); p = mul( float2x2( 0.80, 0.60, -0.60, 0.80 ), p ) * 2.03;
		f += 0.1250 * SimplexNoise2D( p );
		return f / 0.875;
	}

	// Mathematical Curl Field for True Fluid Incompressibility
	float2 Curl2D( float2 p )
	{
		const float eps = 0.02;
		float n1 = FbmNoise( p + float2( 0.0, eps ) );
		float n2 = FbmNoise( p - float2( 0.0, eps ) );
		float n3 = FbmNoise( p + float2( eps, 0.0 ) );
		float n4 = FbmNoise( p - float2( eps, 0.0 ) );
		return float2( ( n1 - n2 ) / ( 2.0 * eps ), -( n3 - n4 ) / ( 2.0 * eps ) );
	}

	// Hardware Screen-Derivative Anti-Aliased Step (Crisp Anime Cel Edges)
	float StepAA( float threshold, float value, float crispness )
	{
		float afwidth = length( float2( ddx( value ), ddy( value ) ) ) * ( 0.7071 / max( 0.1, crispness ) );
		return smoothstep( threshold - afwidth, threshold + afwidth, value );
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

		// Thermal Buoyancy Pulse Expansion
		float thermalLift = sin( g_flTime * ( g_flAscentSpeed * 2.5 ) + i.vPositionOs.z * 0.15 );
		i.vPositionOs.z += max( 0.0, thermalLift ) * 0.18;
		i.vPositionOs.xy += i.vNormalOs.xy * ( thermalLift * 0.08 * progress );

		// Aerodynamic drag
		i.vPositionOs.xy -= clamp( g_vMotionVelocity.xy, -20.0, 20.0 ) * ( progress * 0.25 );

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
		float flowRate = g_flTime * g_flAscentSpeed * 0.8;

		// 1. Symmetrical Center-Anchored Fluid Advection (Curl Flow)
		float2 centeredUV = float2( ( uv.x - 0.5 ) * g_flBuoyancySpread + 0.5, uv.y * 1.5 - flowRate );
		float2 fluidVelocity = Curl2D( centeredUV * 1.2 ) * ( g_flCurlTurbulence * 0.12 );
		float2 warpedUV = centeredUV + fluidVelocity;

		// 2. High-Frequency Flame Tongue Filaments
		float tongueA = FbmNoise( float2( warpedUV.x * 2.5, warpedUV.y * 0.9 ) );
		float tongueB = SimplexNoise2D( float2( warpedUV.x * 5.0 + flowRate * 0.3, warpedUV.y * 1.6 ) );
		float filamentField = saturate( tongueA * 0.65 + tongueB * 0.35 );

		// 3. Rayleigh-Taylor Vortex Shedding & Droplet Pinch-Off
		float pinchWave = sin( uv.y * 12.0 * g_flPinchOffFrequency - flowRate * 4.0 );
		float neckingFactor = 1.0 - saturate( pinchWave * ( g_flPinchOffIntensity * 0.45 ) * uv.y );
		filamentField *= neckingFactor;

		// Detached Plasma Droplet Generation
		float dropletNoise = SimplexNoise2D( float2( uv.x * 6.0, uv.y * 3.0 - flowRate * 1.2 ) );
		float detachedDroplets = StepAA( 0.70, dropletNoise * saturate( uv.y * 1.4 ), g_flCelEdgeCrispness );

		// 4. Full-Width Combustion Core & Vertical Decay
		float verticalDecay = saturate( 1.0 - pow( uv.y / max( 0.2, g_flFlameTaper * 0.7 ), 1.3 ) );
		float analyticalCore = pow( verticalDecay, 1.2 ) * 0.6;

		// Total Continuous Thermal Heat Energy [0.0 - 1.0] (Spans entire X width)
		float heatEnergy = saturate( ( filamentField * verticalDecay + analyticalCore ) * 1.5 + detachedDroplets * 0.35 );

		// 5. Anti-Aliased Cel-Shaded Isotherm Quantization (The "Lofi HD" Look)
		float tier0_Smoke   = StepAA( 0.06, heatEnergy, g_flCelEdgeCrispness ); // Outer boundary
		float tier1_Outer   = StepAA( 0.24, heatEnergy, g_flCelEdgeCrispness ); // Combustion Shell
		float tier2_Inner   = StepAA( 0.52, heatEnergy, g_flCelEdgeCrispness ); // Plasma Zone
		float tier3_Core    = StepAA( 0.78, heatEnergy, g_flCelEdgeCrispness ); // White-Hot Core

		// 6. High-Velocity Anisotropic Spark Streaks
		float2 sparkGrid = float2( uv.x * 24.0, ( uv.y - flowRate * 2.2 ) * ( 30.0 / g_flSparkElongation ) );
		float2 sparkRand = Hash22( floor( sparkGrid ) );
		float2 sparkFract = frac( sparkGrid ) - 0.5;
		sparkFract.y *= ( 1.0 / g_flSparkElongation );
		
		float sparkDist = length( sparkFract - sparkRand * 0.2 );
		float sparkMask = StepAA( 0.08, 0.12 - sparkDist, g_flCelEdgeCrispness * 2.0 );
		float sparkFlicker = sin( g_flTime * 35.0 + sparkRand.x * 50.0 ) * 0.5 + 0.5;
		float sparks = sparkMask * sparkFlicker * g_flSparkDensity * verticalDecay;

		// 7. Spectral Composition (Soot -> Outer Flame -> Plasma -> Core)
		float3 baseFlame = lerp( g_vSootSmokeColor, g_vOuterCombustionColor, tier1_Outer );
		baseFlame = lerp( baseFlame, g_vInnerPlasmaColor, tier2_Inner );
		float3 finalFlameColor = lerp( baseFlame, g_vWhiteHotCoreColor, tier3_Core );

		// Spark highlights
		finalFlameColor = lerp( finalFlameColor, g_vWhiteHotCoreColor, sparks );

		// Compute self-illumination emission
		float emissionMask = saturate( tier1_Outer * 0.4 + tier2_Inner * 0.7 + tier3_Core * 1.5 + sparks * 3.0 );
		float3 emissionOutput = finalFlameColor * emissionMask * g_flEmissionOverdrive;

		// 8. Total Opacity & Soft Depth Intersection
		float totalOpacity = saturate( tier0_Smoke * 0.95 + sparks );

		#if defined( S_TRANSLUCENT ) && S_TRANSLUCENT
			float flSceneDepth = Depth::GetLinear( i.vPositionSs.xy );
			float flDepthFade = saturate( ( flSceneDepth - i.vPositionSs.w ) / 10.0 );
			totalOpacity *= flDepthFade;
		#endif

		// 9. Feed S&box PBR Pipeline
		m.Albedo = g_vSootSmokeColor * ( 1.0 - tier2_Inner );
		m.Emission = emissionOutput;
		m.Opacity = totalOpacity;
		m.Roughness = lerp( 0.95, 0.2, tier2_Inner );
		m.Metalness = 0.0;

		return ShadingModelStandard::Shade( i, m );
	}
}