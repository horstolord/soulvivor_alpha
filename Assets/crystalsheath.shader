HEADER
{
	Description = "Soulvivor Permafrost Geode - Earth & Crystal Spike Weapon Sheath";
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

	// ── Buff Activation ───────────────────────────────────────────────────────
	float  g_flBuffProgress       < UiGroup( "Buff Control,10/1" );   UiType( Slider ); Range( 0.0, 1.0 );   Default( 1.0 ); >;
	float  g_flCrystalGrowthSpeed < UiGroup( "Buff Control,10/2" );   UiType( Slider ); Range( 0.5, 5.0 );   Default( 2.0 ); >;

	// ── Spike & Crystal Geometry ──────────────────────────────────────────────
	float  g_flSpikeDensity       < UiGroup( "Spike Geometry,15/1" ); UiType( Slider ); Range( 5.0, 50.0 );  Default( 22.0 ); >;
	float  g_flSpikeExtrusion     < UiGroup( "Spike Geometry,15/2" ); UiType( Slider ); Range( 0.0, 3.0 );   Default( 0.85 ); >;
	float  g_flSpikeSharpness     < UiGroup( "Spike Geometry,15/3" ); UiType( Slider ); Range( 0.5, 4.0 );   Default( 2.2 ); >;
	float  g_flCragRoughness      < UiGroup( "Spike Geometry,15/4" ); UiType( Slider ); Range( 0.0, 1.0 );   Default( 0.35 ); >;

	// ── Color & Material Tints ────────────────────────────────────────────────
	float3 g_vBasaltRockColor     < UiGroup( "Material Palette,20/1" ); UiType( Color );  Default3( 0.12, 0.13, 0.16 ); >;
	float3 g_vCrystalTipColor     < UiGroup( "Material Palette,20/2" ); UiType( Color );  Default3( 0.45, 0.88, 1.00 ); >;
	float3 g_vFrostRimeColor      < UiGroup( "Material Palette,20/3" ); UiType( Color );  Default3( 0.90, 0.96, 1.00 ); >;
	float3 g_vGeodeCoreEmission   < UiGroup( "Material Palette,20/4" ); UiType( Color );  Default3( 0.20, 0.70, 0.95 ); >;
	float  g_flGeodeGlowIntensity < UiGroup( "Material Palette,20/5" ); UiType( Slider ); Range( 0.0, 6.0 );   Default( 2.5 ); >;

	// Procedural Math Functions
	float2 Hash22( float2 p )
	{
		float3 p3 = frac( float3( p.xyx ) * float3( 0.1031, 0.1030, 0.0973 ) );
		p3 += dot( p3, p3.yzx + 33.33 );
		return frac( ( p3.xx + p3.yz ) * p3.zy ) * 2.0 - 1.0;
	}

	float4 Voronoi2D( float2 x )
	{
		float2 n = floor( x );
		float2 f = frac( x );
		float f1 = 8.0;
		float f2 = 8.0;
		float2 mg = float2( 0.0, 0.0 );
		float2 mr = float2( 0.0, 0.0 );

		[unroll]
		for ( int j = -1; j <= 1; j++ )
		{
			[unroll]
			for ( int i = -1; i <= 1; i++ )
			{
				float2 g = float2( float( i ), float( j ) );
				float2 o = Hash22( n + g ) * 0.5 + 0.5;
				float2 r = g - f + o;
				float d = dot( r, r );

				if ( d < f1 )
				{
					f2 = f1;
					f1 = d;
					mg = g;
					mr = r;
				}
				else if ( d < f2 )
				{
					f2 = d;
				}
			}
		}
		// Returns: x = Dist to center, y = 2nd dist, zw = Cell ID
		return float4( sqrt( f1 ), sqrt( f2 ), n + mg );
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
		// 1. Crystallization Wipe Front (Spreads along weapon length uv.y)
		float growthProgress = saturate( ( g_flBuffProgress * 1.3 ) - ( i.vTexCoord.y * 0.3 ) );
		float growthWave = smoothstep( 0.05, 0.95, growthProgress );

		// 2. Voronoi Pyramidal Needle Extrusion
		float4 vor = Voronoi2D( i.vTexCoord.xy * g_flSpikeDensity );
		
		// Sharp pyramidal needle: Peak height at cell center (vor.x = 0), zero at cell edges
		float needleTip = saturate( 1.0 - vor.x );
		needleTip = pow( needleTip, g_flSpikeSharpness );

		// Random cell height variation (so some crystals are long needles, others shorter crags)
		float cellHash = frac( sin( dot( vor.zw, float2( 12.9898, 78.233 ) ) ) * 43758.5453 );
		float spikeHeight = needleTip * ( 0.4 + cellHash * 0.6 );

		// Extrude vertices physically along the normal
		float finalExtrusion = spikeHeight * g_flSpikeExtrusion * growthWave;
		i.vPositionOs.xyz += i.vNormalOs.xyz * finalExtrusion;

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

		// 1. Buff Propagation Front
		float growthProgress = saturate( ( g_flBuffProgress * 1.3 ) - ( uv.y * 0.3 ) );
		float crystallizationWave = smoothstep( 0.05, 0.95, growthProgress );

		// 2. Cellular Crystal Spikes & Facets
		float4 vor = Voronoi2D( uv * g_flSpikeDensity );
		float needleTip = saturate( 1.0 - vor.x );
		needleTip = pow( needleTip, g_flSpikeSharpness );

		// Crystal cell borders (crevices between spikes)
		float creviceDist = vor.y - vor.x;
		float creviceMask = 1.0 - smoothstep( 0.01, 0.15, creviceDist );

		// 3. Faceted Normal Perturbation (For sharp crystal specular glints)
		float2 facetSlope = ( frac( uv * g_flSpikeDensity ) - 0.5 ) * 2.0;
		float3 crystalNormal = normalize( i.vNormalWs + float3( facetSlope.x, facetSlope.y, 0.0 ) * 0.75 );

		// 4. Earth-to-Ice Material Transition
		// Base: Deep dark basalt / slate
		float3 baseEarth = g_vBasaltRockColor;

		// Middle to Tip: Transition from rocky base to glacial quartz tips
		float3 crystalTip = lerp( baseEarth, g_vCrystalTipColor, needleTip );

		// Crevices: Filled with bright white frost rime
		float3 surfaceAlbedo = lerp( crystalTip, g_vFrostRimeColor, creviceMask * 0.75 );

		// 5. Geode Core Internal Glow (Emits from deep within the crystalline core)
		float3 viewDir = normalize( -i.vPositionWithOffsetWs.xyz );
		float rim = 1.0 - saturate( abs( dot( crystalNormal, viewDir ) ) );
		
		// Internal rune/pulse energy
		float pulse = sin( g_flTime * 4.0 + uv.y * 10.0 ) * 0.3 + 0.7;
		float3 coreEmission = g_vGeodeCoreEmission * pow( rim, 2.5 ) * g_flGeodeGlowIntensity * pulse;
		coreEmission *= ( needleTip * 0.8 + 0.2 ) * crystallizationWave;

		// 6. Opacity & Soft Depth Intersect
		// The sheath starts transparent and solidifies as buff progress increases
		float totalOpacity = saturate( ( 0.75 + needleTip * 0.25 ) * crystallizationWave );

		#if defined( S_TRANSLUCENT ) && S_TRANSLUCENT
			float flSceneDepth = Depth::GetLinear( i.vPositionSs.xy );
			float flDepthFade = saturate( ( flSceneDepth - i.vPositionSs.w ) / 6.0 );
			totalOpacity *= flDepthFade;
		#endif

		// 7. Feed S&box PBR Engine
		m.Albedo = surfaceAlbedo;
		m.Normal = crystalNormal;
		
		// Rocky base is matte (roughness 0.85); crystalline needle tips are glassy/specular (roughness 0.08)
		m.Roughness = lerp( 0.85, 0.08, needleTip );
		m.Metalness = 0.0;
		m.AmbientOcclusion = saturate( 1.0 - creviceMask * 0.8 );
		m.Emission = coreEmission;
		m.Opacity = totalOpacity;

		return ShadingModelStandard::Shade( i, m );
	}
}