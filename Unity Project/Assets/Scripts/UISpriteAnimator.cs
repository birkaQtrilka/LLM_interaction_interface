using UnityEngine;
using UnityEngine.UI;

public class UISpriteAnimator : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Sprite[] frames;

    [SerializeField] private float framesPerSecond = 12f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool enableDisableOnRun = true;
    private int currentFrame;
    private float timer;
    private bool playing;

    private void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();
        if(enableDisableOnRun) image.enabled = false;
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (!playing || frames == null || frames.Length == 0)
            return;

        timer += Time.deltaTime;

        float frameDuration = 1f / framesPerSecond;

        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = frames.Length - 1;
                    playing = false;
                }
            }

            image.sprite = frames[currentFrame];
        }
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0)
            return;
        playing = true;
        if (enableDisableOnRun) image.enabled = true;

    }

    public void Stop()
    {
        playing = false;
        if (enableDisableOnRun) image.enabled = false;

    }

    public void Restart()
    {
        Stop();
        image.sprite = frames[0];
        currentFrame = 0;
        timer = 0f;
        Play();
    }
}