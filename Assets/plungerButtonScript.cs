using System;
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

    private KMBombModule module;
    private KMAudio audio;

    public Material[] discoColours;
    public Renderer surface;
    public Material blackMat;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    private int rule;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        button.OnInteract += delegate () { PressButton(); return false; };
        button.OnInteractEnded += delegate () { ReleaseButton();};
        module = GetComponent<KMBombModule>();
        audio = GetComponent<KMAudio>();
    }

    void Start()
    {
        materialCubes.SetActive(false);
        unlit = Bomb.GetOffIndicators().Count();
        lit = Bomb.GetOnIndicators().Count();
        // negative returns -1, equal returns 0, positive returns 1
        rule = Math.Sign(lit - unlit) + 1;
        var sym = new [] { "<", "=", ">" };
        Debug.LogFormat("[The Plunger Button #{0}] Lit ({2}) {1} unlit ({3}).", moduleId, sym[rule], unlit, lit);
    }

    int SolvedModules() 
    {
        return Bomb.GetSolvedModuleNames().Count() % 10;
    }

    int GetSecond()
    {
        return Mathf.FloorToInt(Bomb.GetTime() % 60 % 10);
    }

    int TargetHold(int s)
    {
        // lit > unlit
        if (rule == 2)
            return pressTimeOptionsLit[s];
        return pressTimeOptionsUnlit[s];
    }

    int TargetRelease(int s)
    {
        // unlit > lit
        if (rule == 0)
            return releaseTimeOptionsUnlit[s];
        return releaseTimeOptionsLit[s];
    }

    public void PressButton()
    {
        if(moduleSolved)
        {
            return;
        }
        var solvedModules = SolvedModules();
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
        button.AddInteractionPunch();
        timeOfPress = GetSecond();
        targetPressTime = TargetHold(solvedModules);
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
        var solvedModules = SolvedModules();
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
        button.AddInteractionPunch();
        timeOfRelease = GetSecond();
        targetReleaseTime = TargetRelease(solvedModules);
        Debug.LogFormat("[The Plunger Button #{0}] Current solved modules modulo 10: {1}. Target release time: {2}. Actual release time: {3}.", moduleId, solvedModules, targetReleaseTime, timeOfRelease);
        buttonAnimation.SetBool("press", false);
        buttonAnimation.SetBool("release", true);
        pressed = false;
        CheckInput();
    }

    void PanicAtThe()
    {
        Debug.LogFormat("<The Plunger Button #{0}> The module has been prematurely solved.", moduleId);
        moduleSolved = true;
        module.HandlePass();
        surface.material = blackMat;
        StopAllCoroutines();
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
            module.HandlePass();
        }
        else
        {
            Debug.LogFormat("[The Plunger Button #{0}] Strike! Incorrect response.", moduleId);
            module.HandleStrike();
        }
    }

    private string TwitchHelpMessage = "Hold and release the button by using !{0} hold on 0, release on 0";

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var match = Regex.Match(command, "^(hold|release) on ([0-9])$");
        var chainMatch = Regex.Match(command, "^(hold on|submit) ([0-9])(?:;|,|)(?: |)(?:release on |)([0-9])$");
        var isHeld = match.Success ? match.Groups[1].Value == "release" : false;
        if (!match.Success && !chainMatch.Success)
            yield break;
        if (match.Success && isHeld != pressed)
        {
            string error = pressed ? "is already being held!" : "has not been held yet.";
            yield return "sendtochaterror The Plunger Button " + error;
            yield break;
        }
        yield return null;
        var time = match.Success ? match.Groups[2].Value : chainMatch.Groups[2].Value;
        while (GetSecond() != int.Parse(time))
            yield return string.Format("trycancel The Plunger Button was not {0} due to a request to cancel.", pressed ? "released" : "held");
        if (chainMatch.Success)
        {
            yield return button;
            time = chainMatch.Groups[3].Value;
            while (GetSecond() != int.Parse(time))
                yield return string.Format("trycancel The Plunger Button was not released due to a request to cancel.");
        }
        yield return button;
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if (!pressed)
        {
            while (GetSecond() != TargetHold(SolvedModules()))
                yield return true;
            button.OnInteract();
        }
        if (timeOfPress != targetPressTime)
        {
            // The module is in an unsolvable state and so we panic and halt the autosolver.
            // In TP terms this just means HandlePass and yield break.
            PanicAtThe();
            yield break;
        }
        while (GetSecond() != TargetRelease(SolvedModules()))
            yield return true;
        button.OnInteractEnded();
    }
}
