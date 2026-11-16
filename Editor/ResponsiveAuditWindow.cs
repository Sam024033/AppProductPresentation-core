using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Screen = UnityEngine.Device.Screen;

namespace Liberty3D.ProductViewer.Editor
{
    public class ResponsiveAuditWindow : EditorWindow
    {
        readonly List<string> messages = new List<string>();
        Vector2 scroll;
        [MenuItem("Tools/Liberty 3D/UI/Diagnostiquer l'affichage responsive")]
        public static void Open() => GetWindow<ResponsiveAuditWindow>("Diagnostic responsive");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("En Play : ouvre l'ecran a verifier, choisis un appareil et une orientation dans Device Simulator, puis lance le diagnostic. Recommence pour chaque ecran et orientation.", MessageType.Info);
            if (GUILayout.Button("Analyser l'ecran actif", GUILayout.Height(32))) Analyze();
            if (GUILayout.Button("Ouvrir le Device Simulator")) EditorApplication.ExecuteMenuItem("Window/General/Device Simulator");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var message in messages) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("Controle indicatif : il ne verifie pas le rendu esthetique, tous les chevauchements, le PDF, les gestes ni les performances. Confirme sur appareil reel.", MessageType.None);
        }

        void Analyze()
        {
            messages.Clear();
            if (!EditorApplication.isPlaying) { messages.Add("Demarre Play pour analyser l'interface reelle."); return; }
            var viewer = Object.FindFirstObjectByType<ProductViewer>();
            if (!viewer || !viewer.Interface) { messages.Add("Interface introuvable : verifier la scene, le catalogue et la Console."); return; }
            var ui = viewer.Interface; var canvas = ui.canvas;
            if (!canvas || !canvas.isActiveAndEnabled) { messages.Add("Canvas absent ou desactive."); return; }
            if (!EventSystem.current) messages.Add("ATTENTION : aucun EventSystem actif pour les boutons.");
            var safe = Screen.safeArea;
            messages.Add("Etat : " + viewer.State + " | Ecran : " + Screen.width + " x " + Screen.height + " | Zone sure : " + safe);
            messages.Add("Canvas : " + canvas.renderMode + " | echelle : " + canvas.scaleFactor.ToString("F2") + " | racine : " + ui.transform.localScale);
            if (Screen.width <= 0 || Screen.height <= 0 || safe.width <= 0 || safe.height <= 0) { messages.Add("ATTENTION : dimensions ou zone sure nulles."); return; }
            var buttons = ui.GetComponentsInChildren<Selectable>(false);
            messages.Add("Controles actifs : " + buttons.Length);
            if (buttons.Length == 0) messages.Add("ATTENTION : aucun controle actif visible dans ViewerUI.");
            var corners = new Vector3[4]; int outside = 0;
            foreach (var control in buttons)
            {
                if (!control.isActiveAndEnabled || !(control.transform is RectTransform rect)) continue;
                rect.GetWorldCorners(corners);
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                if (min.x < safe.xMin - 1 || min.y < safe.yMin - 1 || max.x > safe.xMax + 1 || max.y > safe.yMax + 1)
                { outside++; messages.Add("Hors zone sure (a verifier) : " + Path(control.transform)); }
            }
            messages.Add(outside == 0 ? "Aucun controle entierement ou partiellement hors zone sure detecte." : outside + " controle(s) a examiner ; certains peuvent etre dans une liste defilante.");
        }

        static string Path(Transform transform)
        {
            string path = transform.name;
            while (transform.parent) { transform = transform.parent; path = transform.name + "/" + path; }
            return path;
        }
    }
}
