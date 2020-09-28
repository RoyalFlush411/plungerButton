using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using plungerButton;

public class plungerButtonScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable button;
    public Animator buttonAnimation;
    public GameObject materialCubes;

    private int solvedModules = 0;
    private float timeOfPress = 0f;
    private float timeOfRelease = 0f;
    public int[] pressTimeOptionsLit;
    public int[] pressTimeOptionsUnlit;
    public int[] releaseTimeOptionsUnlit;
    public int[] releaseTimeOptionsLit;

    private int unlit = 0;
    private int lit = 0;
    private int targetPressTime = 0;
    private int targetReleaseTime = 0;
    private bool pressed;

    public Material[] discoColours;
    public Renderer surface;
    public Material blackMat;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        button.OnInteract += delegate () { PressButton(); return false; };
        button.OnInteractEnded += delegate () { ReleaseButton();};
    }

    void Update()
    {
        solvedModules = (Bomb.GetSolvedModuleNames().Count()) % 10;
        if(lit > unlit)
        {
            for(int i = 0; i <= 9; i++)
            {
                if(solvedModules == i)
                {
                    targetPressTime = pressTimeOptionsLit[i];
                }
            }
        }
        else
        {
            for(int i = 0; i <= 9; i++)
            {
                if(solvedModules == i)
                {
                    targetPressTime = pressTimeOptionsUnlit[i];
                }
            }
        }

        if(unlit > lit)
        {
            for(int i = 0; i <= 9; i++)
            {
                if(solvedModules == i)
                {
                    targetReleaseTime = releaseTimeOptionsUnlit[i];
                }
            }
        }
        else
        {
            for(int i = 0; i <= 9; i++)
            {
                if(solvedModules == i)
                {
                    targetReleaseTime = releaseTimeOptionsLit[i];
                }
            }
        }
    }

    void Start()
    {
        materialCubes.SetActive(false);
        unlit = Bomb.GetOffIndicators().Count();
        lit = Bomb.GetOnIndicators().Count();
        if(unlit > lit)
        {
            Debug.LogFormat("[The Plunger Button #{0}] Unlit ({1}) > lit ({2}).", moduleId, unlit, lit);
        }
        else if(lit > unlit)
        {
            Debug.LogFormat("[The Plunger Button #{0}] Lit ({1}) > unlit ({2}).", moduleId, lit, unlit);
        }
        else
        {
            Debug.LogFormat("[The Plunger Button #{0}] Lit ({1}) = unlit ({2}).", moduleId, lit, unlit);
        }
    }

    public void PressButton()
    {
        if(moduleSolved)
        {
            return;
        }
        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
        GetComponent<KMSelectable>().AddInteractionPunch();
        timeOfPress = (Bomb.GetTime() % 60) % 10;
        timeOfPress = Mathf.FloorToInt(timeOfPress);
        Debug.LogFormat("[The Plunger Button #{0}] Current solved modules: {1}. Target press time: {2}. Actual press time: {3}.", moduleId, solvedModules, targetPressTime, timeOfPress);
        buttonAnimation.SetBool("release", false);
        buttonAnimation.SetBool("press", true);
        pressed = true;
        StartCoroutine(Disco());
    }

    public void ReleaseButton()
    {
        if(moduleSolved || !pressed)
        {
            return;
        }
        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
        GetComponent<KMSelectable>().AddInteractionPunch();
        timeOfRelease = (Bomb.GetTime() % 60) % 10;
        timeOfRelease = Mathf.FloorToInt(timeOfRelease);
        Debug.LogFormat("[The Plunger Button #{0}] Current solved modules modulo 10: {1}. Target release time: {2}. Actual release time: {3}.", moduleId, solvedModules, targetReleaseTime, timeOfRelease);
        buttonAnimation.SetBool("press", false);
        buttonAnimation.SetBool("release", true);
        pressed = false;
        CheckInput();
    }

    IEnumerator Disco()
    {
        while(pressed)
        {
            int index = UnityEngine.Random.Range(0,10);
            surface.material = discoColours[index];
            yield return new WaitForSeconds(0.125f);
        }
        surface.material = blackMat;
    }

    void CheckInput()
    {
        if(targetPressTime == timeOfPress && targetReleaseTime == timeOfRelease)
        {
            moduleSolved = true;
            Debug.LogFormat("[The Plunger Button #{0}] Correct response. Module solved.", moduleId);
            GetComponent<KMBombModule>().HandlePass();
        }
        else
        {
            Debug.LogFormat("[The Plunger Button #{0}] Strike! Incorrect response.", moduleId);
            GetComponent<KMBombModule>().HandleStrike();
        }
    }

    private string TwitchHelpMessage = "Hold and release the button by using !{0} hold on 0, release on 0";

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var match = Regex.Match(command, "^(hold|release) on ([0-9])$");
        var chainMatch = Regex.Match(command, "^(hold on|submit) ([0-9])(?:;|,|)(?: |)(?:release on |)([0-9])$");
        if (!match.Success && !chainMatch.Success)
            yield break;
        yield return null;
        var time = match.Success ? match.Groups[2].Value : chainMatch.Groups[2].Value;
        while (Mathf.FloorToInt(Bomb.GetTime() % 60 % 10) != int.Parse(time))
            yield return string.Format("trycancel The Plunger button was not {0} due to a request to cancel.", pressed ? "released" : "pressed");
        if (chainMatch.Success)
        {
            yield return button;
            time = chainMatch.Groups[3].Value;
            while (Mathf.FloorToInt(Bomb.GetTime() % 60 % 10) != int.Parse(time))
                yield return string.Format("trycancel The Plunger button was not released due to a request to cancel.");
        }
        yield return button;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if (pressed)
        {
            pressed = !pressed;
            GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
            button.AddInteractionPunch();
            buttonAnimation.SetBool("press", false);
            buttonAnimation.SetBool("release", true);
        }
        yield return null;
        yield return HoldButton(ProcessTwitchCommand("hold on " + targetPressTime), true);
        yield return HoldButton(ProcessTwitchCommand("release on " + targetReleaseTime), false);
    }

    IEnumerator HoldButton(IEnumerator coroutine, bool hold)
    {
        while (coroutine.MoveNext())
        {
            var obj = coroutine.Current;
            if (obj is KMSelectable && hold)
                button.OnInteract();
            else if (obj is KMSelectable)
                button.OnInteractEnded();
            yield return obj;
        }
    }
}
