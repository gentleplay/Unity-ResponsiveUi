using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Production contracts for shared Inspector section chrome.
    /// </summary>
    public class ResponsiveUiInspectorChromeTests
    {
    #region Keys

        [Test]
        public void InspectorSectionKey_IncludesEditorAndInstance()
        {
            var go = new GameObject("InspectorChromeA");
            try
            {
                string key = ResponsiveUiInspectorFoldouts.InspectorSectionKey(
                    go, "Hub", "Detection");
                Assert.IsTrue(key.Contains(go.GetInstanceID().ToString()));
                Assert.IsTrue(key.StartsWith("RUI.Hub."));
                Assert.IsTrue(key.EndsWith(".Detection"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InspectorSectionKey_DiffersPerComponent()
        {
            var a = new GameObject("InspectorChromeA");
            var b = new GameObject("InspectorChromeB");
            try
            {
                Assert.AreNotEqual(
                    ResponsiveUiInspectorFoldouts.InspectorSectionKey(a, "Camera", "Cameras"),
                    ResponsiveUiInspectorFoldouts.InspectorSectionKey(b, "Camera", "Cameras"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void InspectorSectionKey_NullTarget_UsesZero()
        {
            Assert.AreEqual(
                "RUI.Hub.0.Detection",
                ResponsiveUiInspectorFoldouts.InspectorSectionKey(null, "Hub", "Detection"));
        }

        [Test]
        public void PropertySectionKey_IncludesInstanceIdAndPath()
        {
            var go = new GameObject("InspectorChromeCam");
            try
            {
                var camera = go.AddComponent<ResponsiveCamera>();
                var so = new SerializedObject(camera);
                SerializedProperty cameras = so.FindProperty("cameras");
                cameras.arraySize = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                SerializedProperty slot = cameras
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("phonePortrait");

                string view = ResponsiveUiInspectorFoldouts.PropertySectionKey(
                    "CamSlot", slot, "View");
                string focus = ResponsiveUiInspectorFoldouts.PropertySectionKey(
                    "CamSlot", slot, "Focus");
                Assert.IsTrue(view.Contains(camera.GetInstanceID().ToString()));
                Assert.IsTrue(view.Contains("phonePortrait"));
                Assert.IsTrue(view.EndsWith(".View"));
                Assert.AreNotEqual(view, focus);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

    #endregion

    #region Layout

        [Test]
        public void SlotSectionHeight_CollapsedIgnoresBody()
        {
            float closed = ResponsiveUiInspectorFoldouts.SlotSectionHeight(false, 120f);
            float openEmpty = ResponsiveUiInspectorFoldouts.SlotSectionHeight(true, 0f);
            float open = ResponsiveUiInspectorFoldouts.SlotSectionHeight(true, 120f);
            Assert.AreEqual(closed, ResponsiveUiInspectorFoldouts.SlotSectionHeight(false, 999f));
            Assert.AreEqual(closed, openEmpty);
            Assert.Greater(open, closed);
        }

        [Test]
        public void AtZeroIndent_RestoresCallerIndent()
        {
            int previous = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 3;
            try
            {
                float height = ResponsiveUiInspectorFoldouts.AtZeroIndent(() =>
                {
                    Assert.AreEqual(0, EditorGUI.indentLevel);
                    return 12f;
                });
                Assert.AreEqual(12f, height);
                Assert.AreEqual(3, EditorGUI.indentLevel);
            }
            finally
            {
                EditorGUI.indentLevel = previous;
            }
        }

        [Test]
        public void FieldLabelWidthFor_WideRow_UsesStandardWidth()
        {
            Assert.AreEqual(
                ResponsiveUiInspectorFoldouts.FieldLabelWidth,
                ResponsiveUiInspectorFoldouts.FieldLabelWidthFor(420f));
        }

        [Test]
        public void FieldLabelWidthFor_NarrowRow_LeavesValueColumn()
        {
            float width = ResponsiveUiInspectorFoldouts.FieldLabelWidthFor(200f);
            Assert.LessOrEqual(
                width + ResponsiveUiInspectorFoldouts.FieldValueMinWidth,
                200f);
            Assert.Greater(width, 70f);
        }

        [Test]
        public void DrawFoldout_DoesNotKeepCallerIndent()
        {
            int previous = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 2;
            try
            {
                MethodInfo draw = typeof(ResponsiveUiInspectorFoldouts).GetMethod(
                    "DrawFoldoutAtZeroIndent",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var fakeFoldout = new Func<bool>(() =>
                {
                    Assert.AreEqual(0, EditorGUI.indentLevel);
                    return false;
                });
                bool open = (bool)draw.Invoke(
                    null,
                    new object[] { fakeFoldout });
                Assert.IsFalse(open);
                Assert.AreEqual(2, EditorGUI.indentLevel);
            }
            finally
            {
                EditorGUI.indentLevel = previous;
            }
        }

    #endregion

    #region Header

        [Test]
        public void HeaderFoldoutPad_IsSixteen()
        {
            Assert.AreEqual(16f, ResponsiveUiInspectorFoldouts.HeaderFoldoutPad);
        }

        [Test]
        public void NestedContentRect_OffsetsByFoldoutArrow()
        {
            var outer = new Rect(40f, 10f, 300f, 80f);
            Rect inner = ResponsiveUiInspectorFoldouts.NestedContentRect(outer);
            Assert.AreEqual(
                outer.x + ResponsiveUiInspectorFoldouts.FoldoutArrowWidth,
                inner.x);
            Assert.AreEqual(
                outer.width - ResponsiveUiInspectorFoldouts.FoldoutArrowWidth,
                inner.width);
            Assert.AreEqual(outer.y, inner.y);
            Assert.AreEqual(outer.height, inner.height);
        }

        [Test]
        public void HeaderFoldoutRect_Tinted_InsetsPastAccent()
        {
            var header = new Rect(20f, 4f, 280f, 22f);
            Rect fold = ResponsiveUiInspectorFoldouts.HeaderFoldoutRect(header, true);
            Assert.AreEqual(
                header.x
                    + ResponsiveUiInspectorFoldouts.SlotSectionAccentWidth
                    + ResponsiveUiInspectorFoldouts.HeaderFoldoutPad,
                fold.x);
            Assert.Greater(fold.x, header.x + ResponsiveUiInspectorFoldouts.SlotSectionAccentWidth);
            Assert.AreEqual(header.y, fold.y);
            Assert.AreEqual(header.height, fold.height);
        }

        [Test]
        public void HeaderContentInset_Tinted_MatchesFoldoutPlusPad()
        {
            Assert.AreEqual(
                ResponsiveUiInspectorFoldouts.HeaderFoldoutPad
                    + ResponsiveUiInspectorFoldouts.SlotSectionAccentWidth
                    + ResponsiveUiInspectorFoldouts.FoldoutArrowWidth,
                ResponsiveUiInspectorFoldouts.HeaderContentInset(true));
        }

        [Test]
        public void HeaderTintInset_Untinted_IsHeaderPad()
        {
            Assert.AreEqual(
                ResponsiveUiInspectorFoldouts.HeaderFoldoutPad,
                ResponsiveUiInspectorFoldouts.HeaderTintInset(false));
            Assert.AreEqual(
                ResponsiveUiInspectorFoldouts.HeaderTintInset(false)
                    + ResponsiveUiInspectorFoldouts.FoldoutArrowWidth,
                ResponsiveUiInspectorFoldouts.HeaderContentInset(false));
        }

    #endregion
    }
}
