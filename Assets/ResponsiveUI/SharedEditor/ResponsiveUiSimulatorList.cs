using System;
using System.Reflection;
using UnityEditor.DeviceSimulation;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>Refreshes open Device Simulator device lists after skin file changes.</summary>
    public static class ResponsiveUiSimulatorList
    {
        #region Refresh

        public static bool Refresh(out string error)
        {
            error = null;
            try
            {
                Type windowType = typeof(DeviceSimulatorPlugin).Assembly.GetType(
                    "UnityEditor.DeviceSimulation.SimulatorWindow");
                if (windowType == null)
                {
                    error = "SimulatorWindow type was not found.";
                    return false;
                }

                MethodInfo markDirty = windowType.GetMethod(
                    "MarkAllDeviceListsDirty",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                markDirty?.Invoke(null, null);

                foreach (UnityEngine.Object window in Resources.FindObjectsOfTypeAll(windowType))
                {
                    object main = ReadMain(windowType, window);
                    if (main == null)
                        continue;

                    MethodInfo update = main.GetType().GetMethod(
                        "UpdateDeviceList",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    update?.Invoke(main, null);

                    FieldInfo dirty = windowType.GetField(
                        "m_DeviceListDirty",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    dirty?.SetValue(window, false);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        #endregion

        #region Helpers

        static object ReadMain(Type windowType, object window)
        {
            PropertyInfo property = windowType.GetProperty(
                "main",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(window);

            FieldInfo field = windowType.GetField(
                "m_Main",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(window);
        }

        #endregion
    }
}
