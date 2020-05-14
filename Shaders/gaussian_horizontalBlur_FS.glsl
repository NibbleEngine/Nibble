/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */



#include "/common.glsl"
#include "/common_structs.glsl"

//Diffuse Textures
uniform sampler2D diffuseTex;

//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};

vec3 gaussianBlur(){
	float kernel[11] = float[] (0.000003, 0.000229, 0.005977, 0.060598,
                                0.24173, 0.382925, 0.24173, 0.060598,
                                 0.005977, 0.000229,0.000003);

	vec3 result = vec3(0.0);
	vec2 offset = vec2(1.0) / (0.5 * mpCommonPerFrame.frameDim);
	vec2 uv = gl_FragCoord.xy / (0.5 * mpCommonPerFrame.frameDim);
    for (int i=-5; i<=5; i++) { 
		//result += kernel[i + 5] * texelFetch(diffuseTex, ivec2(gl_FragCoord.x + i, gl_FragCoord.y), 0).rgb;
		result += kernel[i + 5] * texture(diffuseTex, vec2(uv.x + i * offset.x, uv.y)).rgb;
	}
	return result;
}

void main()
{
	gl_FragColor = vec4(gaussianBlur(), 1.0);
}
