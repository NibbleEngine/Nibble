#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
/* Copies incoming fragment color without change. */
//Diffuse Textures
uniform sampler2DMS diffuseTex;
uniform sampler2DMS positionTex;
uniform sampler2DMS depthTex;

uniform mat4 mvp;
in vec2 uv0;

vec4 worldfromDepth()
{
	vec4 world;
	//world.xy = clipPos.xy / clipPos.w; //tick
	vec2 depth_uv = uv0;
	//depth_uv.y = 1.0 - depth_uv.y;
	//world.z = 0.5 * texture2D(depthTex, depth_uv).r + 1.0; //wrong
	//world.z += 0.5;
	float zNear = 2.0;
	float zFar = 1000.0;
	
	vec4 texelValue = texelFetch(depthTex, ivec2(gl_FragCoord.xy), 0);
	world.z = 2.0 * texelValue.z;
	//world.z = 2.0 * zNear * zFar / (zFar + zNear - world.z * (zFar - zNear));
	
	world.xy = 2.0 * uv0 - 1.0;
	//world.xy = uv0;
	//world.z = 2.0 * texture2D(depthTex, depth_uv).r - 1.0;
	world.w = 1.0f;

	//world = vec4(world.xyz / world.w, world.w);

	//My way
	world = inverse(mvp) * world;
	world.xyz /= world.w;

	return world;
}

void main()
{
	//sample our texture
	vec4 texelValue = texelFetch(diffuseTex, ivec2(gl_FragCoord.xy), 0);
	//vec4 texelValue = texelFetch(depthTex, ivec2(gl_FragCoord.xy), 0);
	//vec4 texelValue = texelFetch(positionTex, ivec2(gl_FragCoord.xy), 0);
	vec4 texColor = vec4(texelValue.rgb, 1.0);
	
	gl_FragColor = texColor;
	
	//gl_FragColor = vec4(texture2D(depthTex, uv0).rrr, 1.0);
	//vec4 world = worldfromDepth();

	//Transform from clip to view space
	//world = inverse(mvp) * world;
	//Fix w
	//world /= world.w;
	
	//gl_FragColor = texture2D(positionTex, uv0) - world;
	//gl_FragColor = vec4(texture2D(positionTex, uv0).rgb, 1.0) - vec4(world.xyz, 1.0);
	//gl_FragColor = vec4(worldfromDepth().zzz, 1.0);
	//gl_FragColor = vec4(1.0, 0.0, 1.0, 1.0);
}
