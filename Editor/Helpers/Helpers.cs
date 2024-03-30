using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace DreadScripts.VRCSDKPlus
{
    internal static class Helpers
    {
        internal static Type ExtendedGetType(string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = assembly.GetTypes();

                type = types.FirstOrDefault(t => t.FullName == typeName);
                if (type == null) type = types.FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }

            return null;
        }

        internal static void RefreshAvatar(ref VRCAvatarDescriptor avatar, ref VRCAvatarDescriptor[] validAvatars, Action OnAvatarChanged = null, Func<VRCAvatarDescriptor, bool> favoredAvatar = null)
        {
            validAvatars = Object.FindObjectsOfType<VRCAvatarDescriptor>();
            if (avatar) return;

            if (validAvatars.Length > 0)
            {
                avatar = favoredAvatar != null ? validAvatars.FirstOrDefault(favoredAvatar) ?? validAvatars[0] : validAvatars[0];
            }

            OnAvatarChanged?.Invoke();
        }

        internal static void GreenLog(string msg)
        {
            Debug.Log($"<color=green>[VRCSDK+] </color>{msg}");
        }
    }
}