using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace DreadScripts.VRCSDKPlus
{
    internal struct ParameterStatus
    {
        internal bool parameterEmpty;
        internal bool parameterAddable;
        internal bool parameterIsDuplicate;
        internal bool hasWarning;
        internal AnimatorControllerParameter matchedParameter;
    }
}