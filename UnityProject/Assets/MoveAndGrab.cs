using UnityEngine;

public class MoveAndGrab : StateMachineBehaviour
{
    [SerializeField] GrabProfile grabProfile;
    AgentSystem system;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (system == null)
        {
            system = FindAnyObjectByType<AgentSystem>();
        }
        if (system == null)
        {
            Debug.LogError("AgentSystem not found in the scene. Please ensure there is an AgentSystem component in the scene.");
            return;
        }

        NPC agent = animator.GetComponentInParent<NPC>();
        GrabIK grabIK = agent.GetComponentInChildren<GrabIK>();

        AnimationInstance animation = system.AnimationLibrary.GetAnimation(x =>
        {
            return x.isPlaying && x.data.name == "use" && x.data.agent == agent.name;
        });
        if (animation == null)
        {
            Debug.LogError($"No 'use' animation found for agent {agent.name}. Please ensure the animation is properly set up.");
            return;
        }
        string useOnName = animation.data.parameters[0];
        Transform useOn = system.GetObject(useOnName)?.transform ?? system.GetAgent(useOnName)?.transform;
        if (useOn == null)
        {
            Debug.LogWarning($"Target {useOnName} not found for use action.");
            return;
        }
        Transform temp = new GameObject("TempUseOn"+ Time.time.ToString("F2")).transform;
        system.contextLibrary.environment.Add(new ContextItem { transform = temp });
        Transform spot = useOn.FindDeep("GrabSpot");
        if (spot == null)
        {
            Debug.LogWarning($"Target {useOnName} does not have a 'GrabSpot' child transform. Using the target's position instead.");
            temp.position = useOn.position;
        }
        else
        {
            temp.position = spot.position;
        }


        agent.StartCoroutine(grabIK.ReachRoutine(
            temp,
            grabProfile,
            () =>
            {
                Animator itemAnim = useOn.GetComponentInChildren<Animator>();
                if (itemAnim != null)
                {
                    itemAnim.SetTrigger("UseLater");
                }
                else
                {
                    Debug.LogWarning($"Item: '{useOn.name}' does not have an Animator component to play the 'Use' animation");
                }
                Destroy(temp.gameObject);
            }
            ));
    }
}
