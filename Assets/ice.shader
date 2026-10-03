HEADER
{
	Description = "Soulvivor Frost with Internal Fractures, Frost & S&box PBR Lighting";
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

	// ── Ice Colors ────────────────────────────────────────────────────────────
	float3 g_vDeepIceColor       < UiGroup( "Color Settings,10/1" ); UiType( Color );  Default3( 0.08, 0.35, 0.55 ); >;
	float3 g_vSurfaceIceColor    < UiGroup( "Color Settings,10/2" ); UiType( Color );  Default3( 0.55, 0.85, 0.98 ); >;
	float3 g_vFrostColor         < UiGroup( "Color Settings,10/3" ); UiType( Color );  Default3( 0.92, 0.97, 1.00 ); >;
	float3 g_vInternalCrackColor < UiGroup( "Color Settings,10/4" ); UiType( Color );  Default3( 0.75, 0.95, 1.00 ); >;

	// ── Ice Structure ─────────────────────────────────────────────────────────
	float  g_flCrackScale        < UiGroup( "Ice Structure,15/1" );  UiType( Slider ); Range( 1.0, 30.0 ); Default( 8.0 ); >;
	float  g_flCrackIntensity    < UiGroup( "Ice Structure,15/2" );  UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.5 ); >;
	float  g_flFrostCoverage     < UiGroup( "Ice Structure,15/3" );  UiType( Slider ); Range( 0.0, 1.0 );  Default( 0.35 ); >;
	float  g_flChiseledFacetBump < UiGroup( "Ice Structure,15/4" );  UiType( Slider ); Range( 0.0, 2.0 );  Default( 0.6 ); >;
	float  g_flSubsurfaceGlow    < UiGroup( "Ice Structure,15/5" );  UiType( Slider ); Range( 0.0, 2.0 );  Default( 0.4 ); >;

	// ── Surface Properties ───────────────────────────────────────────────────
	float  g_flBaseRoughness     < UiGroup( "Surface,20/1" );        UiType( Slider ); Range( 0.0, 1.0 );  Default( 0.08 ); >;
	float  g_flFrostRoughness    < UiGroup( "Surface,20/2" );        UiType( Slider ); Range( 0.0, 1.0 );  Default( 0.75 ); >;
	float  g_flIceOpacity        < UiGroup( "Surface,20/3" );        UiType( Slider ); Range( 0.1, 1.0 );  Default( 0.85 ); >;

	// Procedural Noise Functions
	float2 Hash22( float2 p )
	{
		float3 p3 = frac( p.xyx * float3( 0.1031, 0.1030, 0.0973 ) );
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

	float4 Voronoi2D( float2 x )
	{
		float2 n = floor( x );
		float2 f = frac( x );
		float f1 = 8.0;
		float f2 = 8.0;
		float2 mg = float2( 0.0, 0.0 );

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
				}
				else if ( d < f2 )
				{
					f2 = d;
				}
			}
		}
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
		// Crystalline shard micro-displacement
		float4 vor = Voronoi2D( i.vTexCoord.xy * g_flCrackScale * 0.5 );
		float shardDisplacement = ( vor.y - vor.x ) * 0.15 * g_flChiseledFacetBump;
		i.vPositionOs.xyz += i.vNormalOs.xyz * shardDisplacement;

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
		float3 V = normalize( -i.vPositionWithOffsetWs.xyz ); // Camera direction
		float NdotV = saturate( abs( dot( i.vNormalWs, V ) ) );

		// 1. Internal Crystalline Fractures (Voronoi Ridge Cracks)
		float4 vor = Voronoi2D( uv * g_flCrackScale );
		float crackDist = vor.y - vor.x;
		float internalCracks = 1.0 - smoothstep( 0.01, 0.12, crackDist );
		internalCracks *= g_flCrackIntensity;

		// Secondary micro-shatter lines
		float microFractures = 1.0 - smoothstep( 0.0, 0.08, abs( FbmNoise( uv * g_flCrackScale * 2.5 ) * 2.0 - 1.0 ) );
		float totalCracks = saturate( internalCracks + microFractures * 0.6 );

		// 2. Chiseled Facet Normal Perturbation (For sharp specular glints)
		float2 facetSlope = ( frac( uv * g_flCrackScale ) - 0.5 ) * 1.5;
		float3 iceNormal = normalize( i.vNormalWs + float3( facetSlope.x, facetSlope.y, 0.0 ) * ( 0.4 * g_flChiseledFacetBump ) );

		// 3. Surface Frost Layer
		float frostNoise = FbmNoise( uv * 14.0 );
		float frostMask = smoothstep( 1.0 - g_flFrostCoverage, 1.2 - g_flFrostCoverage, frostNoise + totalCracks * 0.3 );

		// 4. Color & Depth Transition (Deep Glacial Core -> Surface -> Frost)
		float3 iceAlbedo = lerp( g_vDeepIceColor, g_vSurfaceIceColor, pow( 1.0 - NdotV, 1.5 ) );
		iceAlbedo = lerp( iceAlbedo, g_vInternalCrackColor, totalCracks * 0.75 );
		iceAlbedo = lerp( iceAlbedo, g_vFrostColor, frostMask );

		// 5. Simulated Subsurface Light Scatter (Glows when backlit, fades in darkness)
		float3 subsurfaceScatter = g_vSurfaceIceColor * pow( 1.0 - NdotV, 3.0 ) * g_flSubsurfaceGlow;

		// 6. Camera Distance Depth Feather
		float flOpacity = saturate( g_flIceOpacity + frostMask * 0.2 + totalCracks * 0.3 );
		#if defined( S_TRANSLUCENT ) && S_TRANSLUCENT
			float flSceneDepth = Depth::GetLinear( i.vPositionSs.xy );
			float flDepthFade = saturate( ( flSceneDepth - i.vPositionSs.w ) / 8.0 );
			flOpacity *= flDepthFade;
		#endif

		// 7. Feed S&box PBR Material
		m.Albedo = iceAlbedo;
		m.Normal = iceNormal;
		m.Roughness = lerp( g_flBaseRoughness, g_flFrostRoughness, saturate( frostMask + totalCracks * 0.5 ) );
		m.Metalness = 0.0; // Pure dielectric
		m.Emission = subsurfaceScatter;
		m.Opacity = flOpacity;
		m.AmbientOcclusion = saturate( 1.0 - totalCracks * 0.4 );

		// Engine calculates all scene lights, shadows, sun, and sharp reflections
		return ShadingModelStandard::Shade( i, m );
	}
}