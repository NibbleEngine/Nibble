## Home

### Description

**Nibble** is a custom cross-platform graphics (and wannabe game) engine that is *currently* written in C#. Nibble's core code base is derived from the No Man's Sky Model Viewer (NMSMV). At some point so much stuff was put into the viewer that the scope of the project changed and I was more excited to work on a custom rendering/game engine rather than a dedicated asset viewer. The goal is to create a standalone, easy-to-use and portable game engine library that can be integrated with low effort on other projects. 

The engine is designed to be extensible using plugins. This way custom importers/exporters can be plugged into the engine and allow for both the preview of custom formats but also the re-export on other supported model formats.

Since the initial code base was in C#, I did not bother rewriting everything in C++. For this reason I am trying to design engine sub-systems as much as possible in a plug n play way (using abstractions) so that I can provide better implementations at some point in the future and make it work out of the box (hopefully). 

In any case the engine is a heavy WIP for the time being.

### Features

- Crossplatform
- Deferred Rendering (OpenGL 4.6)
- Live Shader (.glsl) editing
- Plugin System for asset importers/exporters
- Custom DDS loading library (DXT10 textures are supported)
- PBR Shading (I have no idea what I am doing >.<)



### Specification
- [Instance Buffer Management](Spec_GLBufferManager.md)
- [Shader System](Spec_ShaderSystem.md)


### API
TODO



### Support or Contact

For questions, suggestions or inquiries please mail me at <gregkwaste@gmail.com>
