/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

 
/* Copies incoming fragment color without change. */


#if defined (_D_VOLUME_TEXTURE)
    #define TEX_SAMPLER(NAME) sampler3D NAME
    #define _3D_SAMPLING
#elif defined(_F55_MULTITEXTURE)
    #define TEX_SAMPLER(NAME) sampler2DArray NAME
    #define _3D_SAMPLING
#else
    #define TEX_SAMPLER(NAME) sampler2D NAME; 
#endif

//Diffuse Textures
uniform TEX_SAMPLER(InTex);
uniform float texture_depth;
uniform float mipmap;
uniform vec4 channelToggle;

in vec2 uv0;
out vec4 fragColour; 

void main()
{
    vec4 color = vec4(0.0);
    #ifdef _3D_SAMPLING
        color = textureLod(InTex, vec3(uv0, texture_depth), mipmap);
    #else
        color = textureLod(InTex, uv0, mipmap);
    #endif
    
    fragColour = channelToggle * color;
}
