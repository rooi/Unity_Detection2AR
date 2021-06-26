Shader "StencilBoolean/StencilMask"
{
    Properties
    {
        _Color("Main Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
 
        _StencilRef0 ("Stencil Reference Value 0", Int) = 0
        _StencilRef1 ("Stencil Reference Value 1", Int) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
 
        Pass {
            Tags { "Queue" = "Transparent" }
 
            Stencil{
                Ref[_StencilRef0]
                Comp Equal
                Pass Replace
            }
 
            ColorMask 0
        }
 
        Pass
        {
            Tags { "Queue" = "Transparent" }
 
            Stencil{
                Ref[_StencilRef1]
                Comp Always
                Pass Replace
            }
 
            Cull Front
 
            ColorMask 0
        }
    }
}