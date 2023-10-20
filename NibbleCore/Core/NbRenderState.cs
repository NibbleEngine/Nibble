using NbCore.Common;

namespace NbCore
{   
    public static class NbRenderState
    {
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
