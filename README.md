# **No Mans's Model Viewer** #
![char.png](http://i.imgur.com/KkotCjBl.png)

No Man's Model Viewer is a model viewer created to preview No Man's Sky 3D Models. It also supports animation playback and also a custom Procedural Generation procedure which tries to emulate the game's behavior during the creation process.

## **Repo Version** ##

* Latest Version 0.4
* [Get latest version](https://bitbucket.org/gregkwaste/nms-viewer/downloads)
* [Wiki Page](https://bitbucket.org/gregkwaste/nms-viewer/wiki/Home)
### Known Issues ###
* So far only diffuse textures are loaded in the viewport from the game files. This is not enough though for many of the models which require also the appropriate mask textures to load the correct alpha channels. I'll be adding support for them in upcoming versions, but for now this may lead to models render in white color in the viewports.
* Especially on ship models, you are going to see small square objects around the ship. These are decals which are supposed to be projected to the main model. Honestly i have no idea how to apply them now, but again I'm definitely interested to find out how they work and hopefully I'll add support for them in the future.

### TODO List ###
* Add support for Mask/Normal Maps
* Add support for custom seeds during the procGen

### How do I get set up? ###

* *Summary of set up*: All you need to do is to build the project's solution file.
* *Configuration*: None
* *Dependencies*: OpenTK is the main dependency for building the project. Also make sure to install the latest NET framework updates and you may also need to update your graphics card drivers. The app also uses the MBINCompiler (https://github.com/emoose/MBINCompiler) for best results always keep a latest version of the MbinCompiler together with the project's main executable.

### Contribution guidelines ###
* Please Report any Issues or any suggestions you may have.

### Who do I talk to? ###

* Send me an email at gregkwaste@gmail.com