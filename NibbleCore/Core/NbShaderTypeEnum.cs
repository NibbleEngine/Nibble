namespace NbCore
{
    public enum NbShaderType
    {
        NULL_SHADER = 0x0,
        MESH_FORWARD_SHADER,
        MESH_DEFERRED_SHADER,
        DECAL_SHADER,
        DEBUG_MESH_SHADER,
        GIZMO_SHADER,
        PICKING_SHADER,
        BBOX_SHADER,
        LOCATOR_SHADER,
        JOINT_SHADER,
        CAMERA_SHADER,
        TEXTURE_MIX_SHADER,
        PASSTHROUGH_SHADER,
        RED_FILL_SHADER,
        LIGHT_SHADER,
        TEXT_SHADER,
        MATERIAL_SHADER,
        GBUFFER_SHADER,
        LIGHT_PASS_LIT_SHADER, //18
        LIGHT_PASS_STENCIL_SHADER,
        LIGHT_PASS_UNLIT_SHADER, //20: Stupid but keeping that for testing...
        BRIGHTNESS_EXTRACT_SHADER,
        DOWNSAMPLING_SHADER, //22 //Used for bloom effect
        UPSAMPLING_SHADER, //Used for bloom effect
        GAUSSIAN_HORIZONTAL_BLUR_SHADER, //24
        GAUSSIAN_VERTICAL_BLUR_SHADER,
        ADDITIVE_BLEND_SHADER, //26
        FXAA_SHADER,
        TONE_MAPPING,//28 
        INV_TONE_MAPPING,
        BWOIT_COMPOSITE_SHADER,//30
        MIX_SHADER,
        GRID_SHADER, //32
        OUTLINE_SHADER
    }
}