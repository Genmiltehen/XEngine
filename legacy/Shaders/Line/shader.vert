#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec4 aColor;

out vec4 vColor;

uniform mat4 uProjection;
uniform mat4 uView;

void main() {
    vColor = aColor;
    gl_Position = uProjection * uView * vec4(aPos, 0.1, 1.0);
}