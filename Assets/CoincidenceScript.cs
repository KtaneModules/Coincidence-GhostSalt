using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using System.Text.RegularExpressions;

public class CoincidenceScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMNeedyModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public TextMesh MainText, SecondaryText;

    private Coroutine[] ButtonAnimCoroutines;
    private Coroutine SolveAnimCoroutine;
    private int[] CurrentNums = new int[4];
    private static readonly int[] ModNums = new[] { 10, 10, 6, 10 };
    private bool Active;

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        ButtonAnimCoroutines = new Coroutine[Buttons.Length];
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate { ButtonPress(x); return false; };
        }
        Module.OnTimerExpired += delegate
        { 
            Module.HandleStrike();
            Audio.PlaySoundAtTransform("strike", Module.transform);
            MainText.text = "";
            Active = false;
            Debug.LogFormat("[Coincidence #{0}] Struck at {1}.", _moduleID, Bomb.GetFormattedTime());
        };
        Module.OnNeedyActivation += delegate
        {
            CurrentNums = new int[4];
            MainText.color = SecondaryText.color = new Color(1, 0, 0, 1);
            MainText.text = "00:00";
            Active = true;
        };
        MainText.text = SecondaryText.text = "";
    }

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        var time = Bomb.GetFormattedTime().Substring(Mathf.Max(Bomb.GetFormattedTime().Length - 5, 0), 5);
        if (time.Contains("."))
            time = "00:" + time.Substring(0, 2);
        if (Active && time == MainText.text)
        {
            Module.HandlePass();
            Audio.PlaySoundAtTransform("solve", Module.transform);
            if (SolveAnimCoroutine != null)
                StopCoroutine(SolveAnimCoroutine);
            SolveAnimCoroutine = StartCoroutine(SolveAnim());
            Active = false;
        }
    }

    void ButtonPress(int pos)
    {
        Audio.PlaySoundAtTransform("press", Buttons[pos].transform);
        if (ButtonAnimCoroutines[pos] != null)
            StopCoroutine(ButtonAnimCoroutines[pos]);
        ButtonAnimCoroutines[pos] = StartCoroutine(ButtonAnim(pos));
        Buttons[pos].AddInteractionPunch();
        if (Active)
        {
            Audio.PlaySoundAtTransform("cycle " + (pos < 4 ? "up" : "down"), Buttons[pos].transform);
            CurrentNums[pos % 4] = (pos < 4 ? (CurrentNums[pos] + 1) : (CurrentNums[pos % 4] + ModNums[pos % 4] - 1)) % ModNums[pos % 4];
            var temp = CurrentNums.Select(x => x.ToString());
            MainText.text = temp.Take(2).Join("") + ":" + temp.Skip(2).Take(2).Join("");
        }
    }

    private IEnumerator SolveAnim(float duration = 0.75f, float secondaryMax = 0.2f)
    {
        MainText.color = new Color(0, 1, 1, 1);
        SecondaryText.color = new Color(0, 1, 1, 0);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            MainText.color = new Color(MainText.color.r, MainText.color.g, MainText.color.b, Mathf.Lerp(1f, 0, Mathf.Min(1f, timer / (duration / 2))));
            var temp = new int[4].Select((x, ix) => Rnd.Range(0, ModNums[ix]).ToString());
            SecondaryText.text = temp.Take(2).Join("") + ":" + temp.Skip(2).Take(2).Join("");
            SecondaryText.color = new Color(SecondaryText.color.r, SecondaryText.color.g, SecondaryText.color.b, Mathf.Sin(Mathf.Lerp(0, Mathf.PI, timer / duration)) * secondaryMax);
        }
        MainText.text = SecondaryText.text = "";
    }

    private IEnumerator ButtonAnim(int pos, float duration = 0.075f, float start = 0.0166f, float depression = 0.002f)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Mathf.Lerp(start, start - depression, timer / duration), Buttons[pos].transform.localPosition.z);
        }
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, Mathf.Lerp(start - depression, start, timer / duration), Buttons[pos].transform.localPosition.z);
        }
        Buttons[pos].transform.localPosition = new Vector3(Buttons[pos].transform.localPosition.x, start, Buttons[pos].transform.localPosition.z);
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} 12:34' to set the display to 12:34.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (command.Length == 4)
            command = '0' + command;
        if (!Active)
        {
            yield return "sendtochaterror I'm not active right now!";
            yield break;
        }
        if (!Regex.IsMatch(command, $@"^[0-{ModNums[0] - 1}][0-{ModNums[1] - 1}]:[0-{ModNums[2] - 1}][0-{ModNums[3] - 1}]$"))
        {
            yield return "sendtochaterror Invalid command.";
            yield break;
        }
        yield return null;
        for (int i = 0; i < 4; i++)
        {
            float timer = 0;
            var num = int.Parse(command[i > 1 ? i + 1 : i].ToString());
            if (num < CurrentNums[i])
                num += ModNums[i];
            var upsNeeded = num - CurrentNums[i];
            if (upsNeeded <= ModNums[i] / 2f)
                for (int j = 0; j < upsNeeded; j++)
                {
                    Buttons[i].OnInteract();
                    timer = 0;
                    while (timer < 0.075f)
                    {
                        yield return null;
                        timer += Time.deltaTime;
                    }
                }
            else
                for (int j = 0; j < ModNums[i] - upsNeeded; j++)
                {
                    Buttons[i + 4].OnInteract();
                    timer = 0;
                    while (timer < 0.075f)
                    {
                        yield return null;
                        timer += Time.deltaTime;
                    }
                }
            timer = 0;
            while (timer < 0.1f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }
}
