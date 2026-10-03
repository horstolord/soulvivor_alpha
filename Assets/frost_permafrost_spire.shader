HEADER
{
	Description = "Soulvivor Glacial Ice with Subsurface Scatter, IOR 1.31 Prism & Thin-Film Glaze (s&box / Source 2)";
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
}

COMMON
{	
	#define S_TRANSLUCENT 1
	#include "common/shared.hlsl"

	float3 g_vGlacialCoreColor     < UiGroup( "Color Settings,10/1" ); UiType( Color );  Default3( 0.35, 0.85, 0.98 ); >;
	float3 g_vFacetEdgeColor       < UiGroup( "Color Settings,10/2" ); UiType( Color );  Default3( 0.92, 0.98, 1.00 ); >;
	float3 g_vFrostRimeColor       < UiGroup( "Color Settings,10/3" ); UiType( Color );  Default3( 0.80, 0.95, 1.00 ); >;
	float3 g_vSpecularGlintColor   < UiGroup( "Color Settings,10/4" ); UiType( Color );  Default3( 1.00, 1.00, 1.00 ); >;

	float  g_flCrystalFacetScale    < UiGroup( "Crystalline Facets,15/1" ); UiType( Slider ); Range( 1.0, 15.0 ); Default( 6.0 ); >;
	float  g_flSubsurfaceScatter    < UiGroup( "Glacial Optics,20/1" );     UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.4 ); >;
	float  g_flIceRefraction        < UiGroup( "Glacial Optics,20/2" );     UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.8 ); >;
	float  g_flDendriticBranching   < UiGroup( "Glacial Optics,20/3" );     UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.5 ); >;
	float  g_flThinFilmIridescence  < UiGroup( "Glacial Optics,20/4" );     UiType( Slider ); Range( 0.0, 3.0 );  Default( 1.2 ); >;
	float  g_flSpecularGlintPower   < UiGroup( "Glacial Optics,20/5" );     UiType( Slider ); Range( 0.5, 5.0 );  Default( 2.5 ); >;

	float3 g_vMotionVelocity        < UiGroup( "Motion Trail,25/5" ); Default3( 0.0, 0.0, 0.0 ); >;

	float2 Hash22( float2 p )
	{
		float3 p3 = frac( p.xyx * float3( 0.1031, 0.1030, 0.0973 ) );
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

	float HexDendrite( float2 p, float scale )
	{
		p *= scale;
		static const float2 k = float2( -0.866025404, 0.5 );
		p = abs( p );
		p -= 2.0 * min( dot( k, p ), 0.0 ) * k;
		float d = length( p - float2( clamp( p.x, -k.y, k.y ), 0.0 ) );
		float branch = sin( p.y * 18.0 + p.x * 12.0 ) * 0.5 + 0.5;
		return ( 1.0 - smoothstep( 0.05, 0.35, d ) ) * branch;
	}

	float3 ThinFilmColor( float cosTheta, float thickness )
	{
		float delta = 2.0 * 1.33 * thickness * cosTheta;
		float3 rgbPhase = float3( 0.0, 0.33, 0.67 );
		return 0.5 + 0.5 * cos( 6.28318 * ( delta + rgbPhase ) );
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
		float4 vor = Voronoi2D( i.vTexCoord.xy * g_flCrystalFacetScale );
		float cellHash = frac( sin( dot( vor.zw, float2( 12.9898, 78.233 ) ) ) * 43758.5453 );
		float steppedHeight = floor( cellHash * 4.0 ) / 4.0;

		// Fixed: Explicit .xyz swizzle to prevent float3 = float4 compiler failure
		i.vPositionOs.xyz += i.vNormalOs.xyz * ( steppedHeight * 0.25 );

		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

PS
{
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	RenderState( DepthWriteEnable, true );
	
	RenderState( CullMode, BACK );

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 uv = i.vTextureCoords.xy;

		float4 vor = Voronoi2D( uv * g_flCrystalFacetScale );
		float borderDist = vor.y - vor.x;
		float crackMask = 1.0 - smoothstep( 0.01, 0.12, borderDist );

		float2 facetSlope = ( frac( uv * g_flCrystalFacetScale ) - 0.5 ) * 2.8;
		float3 facetNormal = normalize( i.vNormalWs + float3( facetSlope.x, facetSlope.y, 0.0 ) );

		// Camera-relative view vector in s&box
		float3 viewDir = normalize( -i.vPositionWithOffsetWs );
		float3 lightDir = normalize( float3( 0.5, 0.8, 0.6 ) );
		float3 halfVec = normalize( lightDir + viewDir );

		float NdotV = saturate( abs( dot( normalize( i.vNormalWs ), viewDir ) ) );
		float NdotH = saturate( dot( facetNormal, halfVec ) );

		// Wrapped BSSRDF Subsurface Scattering
		float wrappedDiffuse = saturate( ( dot( facetNormal, lightDir ) + 0.5 ) / 1.5 );
		float sssDepth = pow( 1.0 - NdotV, 2.2 ) * g_flSubsurfaceScatter;
		float3 sssGlow = lerp( g_vGlacialCoreColor * 0.4, g_vGlacialCoreColor * 1.4, wrappedDiffuse ) * ( sssDepth + 0.3 );

		// Chromatic Prism Refraction (IOR 1.31)
		float rAngle = saturate( dot( normalize( facetNormal + float3( 0.08 * g_flIceRefraction, 0.0, 0.0 ) ), viewDir ) );
		float bAngle = saturate( dot( normalize( facetNormal - float3( 0.08 * g_flIceRefraction, 0.0, 0.0 ) ), viewDir ) );
		float3 prismDispersion = float3( pow( rAngle, 3.0 ), ( rAngle + bAngle ) * 0.3, pow( bAngle, 3.0 ) ) * g_flIceRefraction * 0.4;

		// Nakaya 60° Hexagonal Dendrite Needles
		float dendrite = HexDendrite( uv - float2( 0.5, 0.5 ), 10.0 ) * g_flDendriticBranching;

		// Thin-Film Optical Wave Interference
		float3 iridescentFilm = ThinFilmColor( NdotV, 1.2 + borderDist * 2.0 ) * g_flThinFilmIridescence * 0.4;

		// Beckmann Specular Glints
		float glint = pow( NdotH, 64.0 ) * g_flSpecularGlintPower;

		float3 iceColor = sssGlow + prismDispersion + iridescentFilm;
		iceColor = lerp( iceColor, g_vFacetEdgeColor, crackMask * 0.85 );
		iceColor += dendrite * g_vFrostRimeColor * 0.7;
		iceColor += glint * g_vSpecularGlintColor * 1.8;

		float alpha = saturate( 0.85 + crackMask * 0.15 + glint * 0.8 );

		return float4( iceColor, alpha );
	}
}