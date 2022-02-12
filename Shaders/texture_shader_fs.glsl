/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

 
/* Copies incoming fragment color without change. */

//Diffuse Textures
#ifdef _F55_MULTITEXTURE
uniform sampler2DArray InTex;
#else
uniform sampler2D InTex;
#endif
uniform float texture_depth;
uniform float mipmap;
uniform vec4 channelToggle;

in vec2 uv0;
out vec4 fragColour; 

void main()
{
    vec4 color = vec4(0.0);
    #ifdef _F55_MULTITEXTURE
        color = textureLod(InTex, vec3(uv0, texture_depth), mipmap);
    #else
        color = textureLod(InTex, uv0, mipmap);
    #endif

    fragColour = channelToggle * color;
}
