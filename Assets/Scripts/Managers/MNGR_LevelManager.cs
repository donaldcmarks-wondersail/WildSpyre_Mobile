using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using System;
using Core;
using TMPro;

public class MNGR_LevelManager : Singleton<MNGR_LevelManager>
{
    [System.Serializable]
    public class SceneLoadingVariables
    {
        public string thisScene;
        public string lastScene;
        public string nextScene;
        public string menuScene;
    }

    [System.Serializable]
    public class TimerVariables
    {
        public float levelTimer;
        [HideInInspector]
        public float levelTimerMax;
        [HideInInspector]
        public int levelTimerInt;
        public bool timersActive = true;
    }

    [System.Serializable]
    public class gameWinVariables
    {

    }

    [System.Serializable]
    public class gameLoseVariables
    {

    }

    [System.Serializable]
    public class gamePauseVariables
    {
        public bool isPaused = false;
    }

    [System.Serializable]
    public class UIVariables
    {
        public TextMeshProUGUI timerText;
    }

    [Header("Scene Loading Variables")]
    public SceneLoadingVariables sceneLoadingVars;

    [Header("Timer Variables")]
    public TimerVariables timerVars;

    [Header("Game Win Variables")]
    public gameWinVariables gameWinVars;

    [Header("Game Lose Variables")]
    public gameLoseVariables gameLoseVars;

    [Header("Game Lose Variables")]
    public gamePauseVariables gamePauseVars;

    [Header("Yser Interface Variables")]
    public UIVariables UIVars;

    // Start is called before the first frame update
    void Start()
    {
        sceneLoadingVars.thisScene = SceneManager.GetActiveScene().name;

        timerVars.levelTimerMax = timerVars.levelTimer;
        timerVars.levelTimerInt = (int)Math.Round((double)timerVars.levelTimer, 2);
        updateUIVars();
    }

    // Update is called once per frame
    void Update()
    {
        if (timerVars.timersActive)
        {
            evaluateLevelTimer();
        }
    }

    public void loseGame()
    {

    }

    public void winGame()
    {

    }

    public void evaluateLevelTimer()
    {
        if (!gamePauseVars.isPaused)
        {
            timerVars.levelTimer -= Time.deltaTime;

            timerVars.levelTimerInt = (int)Math.Round((double)timerVars.levelTimer, 2);
            updateUIVars();
        }
    }

    public void updateUIVars()
    {
        if (UIVars.timerText.text != null)
        {
            UIVars.timerText.text = timerVars.levelTimerInt.ToString();
        }
    }

    public void loadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void reloadScene()
    {
        SceneManager.LoadScene(sceneLoadingVars.thisScene);
    }

    public void loadNextScene()
    {
        SceneManager.LoadScene(sceneLoadingVars.nextScene);
    }

}
