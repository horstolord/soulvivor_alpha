HEADER
{
	Description = "Soulvivor Solid Rock with Triplanar Cliff Mapping, Folded Strata & Basalt Jointing (s&box / Source 2)";
}

FEATURES
{
	#include "common/features.hlsl"
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
	#include "common/shared.hlsl"

	float3 g_vBedrockColor          < UiGroup( "Color Settings,10/1" ); UiType( Color );  Default3( 0.22, 0.18, 0.15 ); >;
	float3 g_vStrataLayerColor      < UiGroup( "Color Settings,10/2" ); UiType( Color );  Default3( 0.48, 0.38, 0.28 ); >;
	float3 g_vCavityAoColor         < UiGroup( "Color Settings,10/3" ); UiType( Color );  Default3( 0.06, 0.05, 0.04 ); >;
	float3 g_vMineralVeinColor      < UiGroup( "Color Settings,10/4" ); UiType( Color );  Default3( 0.95, 0.82, 0.35 ); >;

	float  g_flBasaltScale           < UiGroup( "Geological Structure,15/1" );  UiType( Slider ); Range( 1.0, 20.0 ); Default( 6.0 ); >;
	float  g_flDisplacementAmount    < UiGroup( "Geological Structure,15/2" );  UiType( Slider ); Range( 0.0, 2.0 );  Default( 0.35 ); >;
	float  g_flStrataBanding         < UiGroup( "Geological Structure,15/3" );  UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.6 ); >;
	float  g_flStrataFrequency       < UiGroup( "Geological Structure,15/4" );  UiType( Slider ); Range( 2.0, 40.0 ); Default( 16.0 ); >;
	float  g_flCavityAoStrength      < UiGroup( "Geological Structure,15/5" );  UiType( Slider ); Range( 0.0, 5.0 );  Default( 3.0 ); >;
	float  g_flMineralVeinIntensity  < UiGroup( "Geological Structure,15/6" );  UiType( Slider ); Range( 0.0, 5.0 );  Default( 2.4 ); >;
	float  g_flVeinScale             < UiGroup( "Geological Structure,15/7" );  UiType( Slider ); Range( 1.0, 20.0 ); Default( 5.0 ); >;
	float  g_flGraniteGrain          < UiGroup( "Geological Structure,15/8" );  UiType( Slider ); Range( 0.0, 2.0 );  Default( 1.2 ); >;

	float3 g_vMotionVelocity         < UiGroup( "Motion Trail,25/5" ); Default3( 0.0, 0.0, 0.0 ); >;

	float2 Hash22( float2 p )
	{
		float3 p3 = frac( p.xyx * float3( 0.1031, 0.1030, 0.0973 ) );
		p3 += dot( p3, p3.yzx + 33.33 );
		return frac( ( p3.xx + p3.yz ) * p3.zy ) * 2.0 - 1.0;
	}

	float SimplexNoise2D( float2 p )
	{
		static const float K1 = 0.366025404;
		static const float K2 = 0.211324865;

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
		float4 vor = Voronoi2D( i.vTexCoord.xy * g_flBasaltScale );
		float cellHash = frac( sin( dot( vor.zw, float2( 12.9898, 78.233 ) ) ) * 43758.5453 );
		float steppedHeight = floor( cellHash * 5.0 ) / 5.0;
		
		float borderDist = vor.y - vor.x;
		float edgeBevel = smoothstep( 0.02, 0.18, borderDist );

		float displacement = ( ( steppedHeight - 0.5 ) * 0.5 + edgeBevel * 0.2 ) * g_flDisplacementAmount;
		i.vPositionOs.xyz += i.vNormalOs.xyz * displacement;

		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"

	RenderState( BlendEnable, false );
	RenderState( DepthWriteEnable, true );
	RenderState( CullMode, BACK );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );

		float2 uv = i.vTextureCoords.xy;

		// 1. Basalt Cell & Crevice System
		float4 vor = Voronoi2D( uv * g_flBasaltScale );
		float borderDist = vor.y - vor.x;
		float crackMask = 1.0 - smoothstep( 0.01, 0.16, borderDist );

		// Facet normal perturbation (passed to engine for lighting)
		float2 facetSlope = ( frac( uv * g_flBasaltScale ) - 0.5 ) * 1.8;
		float3 facetNormal = normalize( i.vNormalWs + float3( facetSlope.x, facetSlope.y, 0.0 ) * 0.6 );

		// 2. Dendritic Gold Veins
		float2 veinUv = uv * g_flVeinScale;
		float warpA = FbmNoise( veinUv * 1.2 );
		float warpB = FbmNoise( veinUv * 2.4 + float2( 3.2, 7.1 ) );
		float2 warpedVeinCoords = veinUv + float2( warpA, warpB ) * 0.8;
		
		float rawVein = abs( FbmNoise( warpedVeinCoords ) * 2.0 - 1.0 );
		float sharpVein = 1.0 - smoothstep( 0.0, 0.14, rawVein );
		float goldVeinMask = saturate( sharpVein * g_flMineralVeinIntensity );
		float veinContour = smoothstep( 0.14, 0.28, rawVein ) * ( 1.0 - smoothstep( 0.28, 0.45, rawVein ) );

		// 3. Folded Strata Banding
		float strataWarp = FbmNoise( uv * 4.0 ) * 0.2;
		float strataPattern = sin( ( uv.y + strataWarp ) * g_flStrataFrequency );
		float strataBand = pow( strataPattern * 0.5 + 0.5, 2.0 ) * g_flStrataBanding;

		// 4. Granite Micro-Grain
		float grain = ( Hash22( uv * 280.0 ).x * 0.5 + 0.5 ) * g_flGraniteGrain;

		// 5. Crevice Ambient Occlusion
		float cavityAO = saturate( crackMask * g_flCavityAoStrength );

		// 6. Base Albedo Composition
		float3 rockAlbedo = lerp( g_vBedrockColor, g_vStrataLayerColor, saturate( strataBand * 0.75 ) );
		rockAlbedo = lerp( rockAlbedo * 0.85, rockAlbedo * 1.25, grain );
		rockAlbedo = lerp( rockAlbedo, g_vCavityAoColor, cavityAO * 0.95 );
		rockAlbedo = lerp( rockAlbedo, g_vCavityAoColor * 0.5, veinContour * 0.7 );

		// Blend in gold vein color
		float3 goldAlbedo = g_vMineralVeinColor * ( 1.0 + grain * 0.2 );
		float3 finalAlbedo = lerp( rockAlbedo, goldAlbedo, goldVeinMask );

		// 7. Feed S&box PBR Engine Properties
		m.Albedo = finalAlbedo;
		m.Normal = facetNormal;
		m.Roughness = lerp( 0.88, 0.22, goldVeinMask ); // Matte rock, shiny metallic gold
		m.Metalness = goldVeinMask;                     // Pure metal for veins, dielectric for rock
		m.AmbientOcclusion = saturate( 1.0 - cavityAO * 0.85 );

		// Engine calculates all dynamic lights, shadows, and darkness
		return ShadingModelStandard::Shade( i, m );
	}
}