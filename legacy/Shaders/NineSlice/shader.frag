#version 330 core

in vec2 vTexCoord;

out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec2 uTexSize;
uniform vec4 uBorder;
uniform vec2 uRenderSize;
uniform float uAlpha;


void main()
{   
    float l = uBorder.x;
    float r = uBorder.y;
    float t = uBorder.z;
    float b = uBorder.w;

    float w = uTexSize.x;
    float _r = w - r;
    float srcUL = l / w;
    float srcUR = _r / w;

    float h = uTexSize.y;
    float _b = h - b;
    float srcVT = t / h;
    float srcVB = _b / h;
    
    float rw = uRenderSize.x;
    float _rr = rw - r;
    float renUL = l / rw;
    float renUR = _rr / rw;

    float rh = uRenderSize.y;
    float _rb = rh - b;
    float renVT = t / rh;
    float renVB = _rb / rh;

    vec2 uv;

    if (vTexCoord.x < renUL) uv.x = mix(0.0, srcUL, vTexCoord.x / renUL);
    else if (vTexCoord.x > renUR) uv.x = mix(srcUR, 1.0, (vTexCoord.x - renUR) / (1.0 - renUR));
    else {
        float scaleX = (_rr - l) / (_r - l);
        float localU = (vTexCoord.x - renUL) / (renUR - renUL);
        uv.x = mix(srcUL, srcUR, fract(localU * scaleX));
    }

    if (vTexCoord.y < renVT) uv.y = mix(0.0, srcVT, vTexCoord.y / renVT);
    else if (vTexCoord.y > renVB) uv.y = mix(srcVB, 1.0, (vTexCoord.y - renVB) / (1.0 - renVB));
    else {
        float scaleY = (_rb - t) / (_b - t);
        float localV = (vTexCoord.y - renVT) / (renVB - renVT);
        uv.y = mix(srcVT, srcVB, fract(localV * scaleY));
    }

    vec4 texColor = texture(uTexture, uv);
	texColor.a *= uAlpha;
    if (texColor.a < 0.1)
        discard;
    FragColor = texColor;
}