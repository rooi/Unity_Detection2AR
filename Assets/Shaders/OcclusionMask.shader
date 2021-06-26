Shader "Occlusion FX/Occlusion Mask" {
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        Stencil {
            Ref 128
            Comp Always
            Pass Replace
        }
        Pass {
            Fog { Mode Off }
            Color (0,0,0,0)
            ColorMask 0
        }
    }
}