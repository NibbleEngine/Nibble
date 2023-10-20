using NbCore.Common;

namespace NbCore
{   
    public static class NbRenderState
    {
        //Keep the view rotation Matrix
        public static NbMatrix4 rotMat = NbMatrix4.Identity();

        //Keep the view rotation Angles (in degrees)
        public static NbVector3 rotAngles = new NbVector3(0.0f);

        //App Settings
        public static EngineSettings settings;

        //Engine Reference
        public static Engine engineRef;

        //Keep the main camera global
        public static Camera activeCam;
        //Item Counter
        public static int itemCounter = 0;
        //Status
        public static string StatusString = "";

        public static bool enableShaderCompilationLog = true;

        public static ApplicationMode AppMode = ApplicationMode.EDIT;
    }
}
