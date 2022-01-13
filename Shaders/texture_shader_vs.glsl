/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

layout(location = 0) in vec4 vPosition;

uniform float aspect_ratio;
uniform vec2 offset;
uniform float scale;
out vec2 uv0;

void main()
{
    uv0 = vPosition.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);
    //uv0.x = 1.0 - uv0.x; //Mirror on Y
    
    //Render to UV coordinate
    float w,h;
    w = scale * 1.0 * aspect_ratio;
    h = -scale * 1.0;
    mat4 projMat = mat4(1.0/w, 0.0,  0.0, 0.0,
                        0.0, 1.0/h,  0.0, 0.0,
                        0.0, 0.0, -1.0, 0.0,
                        0.0, 0.0,  0.0, 1.0);

    gl_Position = projMat * vec4(vPosition.xy + offset, vPosition.z, 1.0);

}