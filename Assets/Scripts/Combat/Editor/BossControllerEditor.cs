using UnityEngine;
using UnityEditor;

namespace Soulsboss.Combat
{
    [CustomEditor(typeof(BossController))]
    public class BossControllerEditor : Editor
    {
        bool debugFoldout = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var boss = (BossController)target;

            EditorGUILayout.Space(10);
            debugFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(debugFoldout, "Debug — Force Attacks");
            if (debugFoldout)
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to test attacks.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField("State", boss.Current.ToString(), EditorStyles.boldLabel);
                    if (boss.CurrentAttack != null)
                        EditorGUILayout.LabelField("Current Attack", boss.CurrentAttack.GetType().Name);

                    EditorGUILayout.Space(4);

                    if (boss.attacks != null)
                    {
                        for (int i = 0; i < boss.attacks.Count; i++)
                        {
                            var atk = boss.attacks[i];
                            if (atk == null) continue;
                            string label = string.Format("[{0}] {1}", i, atk.GetType().Name);
                            bool busy = boss.Current == BossController.State.Attacking;

                            EditorGUI.BeginDisabledGroup(busy);
                            if (GUILayout.Button(label, GUILayout.Height(28)))
                            {
                                boss.ForceAttack(atk);
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                    }

                    EditorGUILayout.Space(4);

                    if (boss.shield != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Shield UP")) boss.shield.Raise();
                        if (GUILayout.Button("Shield DOWN")) boss.shield.Lower();
                        EditorGUILayout.EndHorizontal();
                    }

                    var counter = boss.GetComponent<BossShieldCounter>();
                    if (counter != null)
                    {
                        EditorGUILayout.Space(4);
                        bool busy = boss.Current == BossController.State.Attacking;
                        EditorGUI.BeginDisabledGroup(busy);
                        if (GUILayout.Button("Trigger Shield Counter", GUILayout.Height(28)))
                        {
                            boss.GetComponent<Health>().OnBlocked?.Invoke();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (Application.isPlaying) Repaint();
        }
    }
}
