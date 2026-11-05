#version 330 core

in vec2 TexCoord;

out vec4 FragColor;

uniform sampler2D uTexture;
uniform float uAlpha;

void main()
{
    vec4 texColor = texture(uTexture, TexCoord);
	texColor.a *= uAlpha;
    if (texColor.a < 0.1)
        discard;

    FragColor = texColor;
}