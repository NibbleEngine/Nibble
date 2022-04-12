## Nibble Shader System

One of the main goals of Nibble since its meant to be a game engine, is live shader editing. This is a hell of a task by itself, and requires a delicate design of all the involved components so that this procedure works without any issues and catastrophic bugs. 

### Description

Shader source files are loaded from disk into `GLSLShaderSource` objects. During initialization file watchers are set for each source file. Each shader source object has a `NbShaderType` attribute that can be used to differentiate between dynamic and static source objects. Dynamic sources correspond to externally loaded source files, while static sources can be used to store static text.

Shader source files have two processing levels: a) Processing and b) Resolution. 

During processing, the actual source text is parsed, directives such as `#include` are recognized. Essentially the source text is broken down to static text parts which get stored into static `GLSLShaderSource` objects and dynamic text parts which hold referce to dynamic `GLSLShaderSource` objects. In the case of dynamic text parts, the engine makes sure to first load and store the source file prior to referencing it.

During resolution, the final source code of the shader source is assembled by carefully combining static and dynamic text parts in the correct order.

When the source file are modified, the corresponding `GLSLShaderSource` object is re-processed and re-resolved. In addition, the `GLSLShaderSource` class includes an `IsUpdated` event delegate that is invoked right after the re-resolution of the object.






### Classes

- [GLSLShaderConfig](#glslshaderconfig)
- [GLSLShaderSource](#glslshadersource)
- [NbShader](#nbshader)
- [MeshMaterial](#meshmaterial)



#### GLSLShaderSource


#### GLSLShaderConfig


#### NbShader


#### MeshMaterial









### Support or Contact
For questions, suggestions or inquiries please mail me at <gregkwaste@gmail.com>
