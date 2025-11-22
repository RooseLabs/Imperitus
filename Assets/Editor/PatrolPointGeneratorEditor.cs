using UnityEngine;
using UnityEditor;
using RooseLabs.Enemies;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(PatrolPointGenerator))]
    public class PatrolPointGeneratorEditor : UnityEditor.Editor
    {
        private PatrolPointGenerator generator;

        private void OnEnable()
        {
            generator = (PatrolPointGenerator)target;
        }

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use these buttons to test patrol point generation in Edit Mode. " +
                "Results will be visible in the Scene view with gizmos enabled.",
                MessageType.Info
            );

            EditorGUILayout.Space(5);

            // Generation button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Patrol Points", GUILayout.Height(30)))
            {
                GenerateInEditor();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(3);

            // Clear button
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Clear Patrol Points", GUILayout.Height(25)))
            {
                ClearInEditor();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            // Stats display
            if (generator.GetEditorPointCount() > 0)
            {
                EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Valid Points: {generator.GetEditorPointCount()}");
                EditorGUILayout.LabelField($"Rejected Points: {generator.GetEditorRejectedCount()}");

                float successRate = generator.GetEditorPointCount() /
                    (float)(generator.GetEditorPointCount() + generator.GetEditorRejectedCount()) * 100f;
                EditorGUILayout.LabelField($"Success Rate: {successRate:F1}%");
            }

            EditorGUILayout.Space(5);

            // Additional options
            EditorGUILayout.LabelField("Visualization", EditorStyles.boldLabel);

            if (GUILayout.Button("Focus Scene View on Points"))
            {
                FocusSceneViewOnPoints();
            }

            // Apply changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }
        }

        private void GenerateInEditor()
        {
            // Record undo
            Undo.RecordObject(generator, "Generate Patrol Points");

            Debug.Log("[PatrolPointGenerator] Starting editor generation...");

            bool success = generator.EditorGeneratePatrolPoints();

            if (success)
            {
                Debug.Log($"[PatrolPointGenerator] Successfully generated {generator.GetEditorPointCount()} points!");
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
            }
            else
            {
                Debug.LogError("[PatrolPointGenerator] Failed to generate patrol points. Check console for details.");
            }
        }

        private void ClearInEditor()
        {
            Undo.RecordObject(generator, "Clear Patrol Points");

            generator.EditorClearPatrolPoints();

            Debug.Log("[PatrolPointGenerator] Cleared all patrol points");
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }

        private void FocusSceneViewOnPoints()
        {
            if (generator.GetEditorPointCount() == 0)
            {
                Debug.LogWarning("No points to focus on! Generate points first.");
                return;
            }

            Bounds bounds = generator.GetEditorPointsBounds();

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Frame(bounds, false);
                sceneView.Repaint();
            }
        }

        // Draw gizmos in scene view
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawGizmosForGenerator(PatrolPointGenerator generator, GizmoType gizmoType)
        {
            // This ensures gizmos are drawn even when not in play mode
            if (!generator.showDebugGizmos)
                return;

            // The actual gizmo drawing is handled by OnDrawGizmos in the main script
        }
    }
}