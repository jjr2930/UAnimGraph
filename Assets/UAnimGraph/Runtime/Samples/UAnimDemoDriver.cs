using UnityEngine;

namespace UAnimGraph
{
    public sealed class UAnimDemoDriver : MonoBehaviour
    {
        public UAnimGraphPlayer player;
        public float phase;
        bool direction;
        void Start()
        {
            if(player && player.asset)
            {
                foreach(var p in player.asset.parameters)
                {
                    if(p.name == "MoveInput" && p.type == UAnimParameterType.Vector2)
                    {
                        direction = true;
                    }
                }
            }
        }

        void Update()
        {
            if(!player || player.Instance == null)
            {
                return;
            }

            player.SetFloat("Speed", Mathf.PingPong(Time.time * .4f + phase, 2));
            if(direction)
            {
                player.SetVector2("MoveInput", new Vector2(Mathf.Sin(Time.time + phase), Mathf.Cos(Time.time + phase)));
            }
        }
    }
}
