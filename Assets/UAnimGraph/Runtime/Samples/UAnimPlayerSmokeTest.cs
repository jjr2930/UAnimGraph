using System;
using UnityEngine;

namespace UAnimGraph
{
    public sealed class UAnimPlayerSmokeTest : MonoBehaviour
    {
        int frames;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(), "-uanimSmokeTest") >= 0)
            {
                new GameObject("UAnimGraph smoke verification").AddComponent<UAnimPlayerSmokeTest>();
            }
        }

        void Update()
        {
            bool full = Array.IndexOf(Environment.GetCommandLineArgs(), "-uanimFullSmokeTest") >= 0;
            if(++frames < (full ? 120 : 30))
            {
                return;
            }

            var players = FindObjectsByType<UAnimGraphPlayer>();
            bool valid = players.Length == 2;
            if(full)
            {
                foreach(var p in players)
                {
                    bool blend = false, code = false;
                    if(p.Instance != null)
                    {
                        foreach(var pose in p.Instance.Poses)
                        {
                            if(pose.PoseDefinition.kind == UAnimPoseKind.Blend2D)
                            {
                                blend = true;
                            }
                        }

                        foreach(var m in p.Instance.Machines)
                        {
                            foreach(var t in m.Definition.transitions)
                            {
                                if(!string.IsNullOrEmpty(t.evaluation))
                                {
                                    code = true;
                                }
                            }
                        }
                    }

                    valid &= blend && code;
                }
            }

            foreach(var p in players)
            {
                valid &= p.Instance != null && p.Instance.Graph.IsValid() && string.IsNullOrEmpty(p.LastError);
            }

            if(valid && players[0].Instance != players[1].Instance)
            {
                Debug.Log(full ? "UANIMGRAPH_COMPLETE_PLAYER_PASS: 120 frames; two independent instances; registered function conditions and Mixer2D loaded" : "UANIMGRAPH_PLAYER_SMOKE_PASS: two independent PlayableGraph instances; 30 frames evaluated");
                Application.Quit(0);
            }
            else
            {
                Debug.LogError("UANIMGRAPH_PLAYER_SMOKE_FAIL");
                Application.Quit(1);
            }

            enabled = false;
        }
    }
}
