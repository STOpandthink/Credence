using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using TagUsage = QuestionsScript.TagUsage;

public class GameScript : MonoBehaviour {
	
	public static GameScript singleton;
	
	public Texture cfarTexture;
	public Material material;
	public GUISkin defaultSkin;
	public GUISkin barSkin;
	
	enum GameStep { Start, Question, Answer, Graph, ExplainScores, EndTutorial, Options, QuestionTypes, QuestionDatabases, DownloadingDatabases, AddingNewDatabase };
	private const int minAnswersForGraph = 5;
	private const int answersBetweenGraph = 5;
	private const int lastXScore = 10;
	private readonly Color answerAColor = new Color(0f / 255f, 204f / 255f, 255f / 255f);
	private readonly Color answerBColor = new Color(255f / 255f, 204f / 255f, 0f / 255f);
	private const string saveGameFilename = "SaveGame";
	private readonly int[] percentages = {50, 60, 70, 80, 90, 99};
	private readonly string[] labelKeysA = {"y", "t", "r", "e", "w", "q"};
	private readonly string[] labelKeysB = {"h", "g", "f", "d", "s", "a"};
	private readonly int[] labelWidths = {17, 22, 17, 15, 17, 17}; // Width of the widest character above, for alignment
	
	private const int answersPerLine = 5;

	GUIStyle highlightedStyle;

	// Current game state.
	GameStep gameStep = GameStep.Start;
	GameStep updatedStep = GameStep.Start;
	List<Answer> answers = new List<Answer>();
	List<Bar> bars = new List<Bar>();
	int questionCount;//questions asked so far
	int currentQuestion;//current question that's being asked
	bool reverseOptions;//if true, B) will be the correct answer
	
	bool tutorialFinished = false;
	bool tutorialAnsweredOneQuestion = false;
	bool tutorialShowScore = false;
	bool restartingGame;
	int selectedBar;//index of the bar selected during the Graph step
	bool gaveCorrectAnswer;
	double probOfCorrectAnswer;
	double totalScore;
	double averageScore;
	
	Vector2 uiScrollPosition;
	bool uiShowAllInstructions = false;
	int uiIntructionsPage = -2;
	
	// Keyboard control game state
	string nextHighlightedKey = "";
	string highlightedKey = "";
	bool waitingForUp = false;
	
	#region Properties
	static bool TouchScreen { get { return Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer; } }
	static string SaveGameFilename { get { return (TouchScreen ? (Application.persistentDataPath + "/") : "") + saveGameFilename; } }
	static string Click { get { return TouchScreen ? "Tap" : "Click"; } }
	GUIStyle WelcomeStyle { get { return GUI.skin.GetStyle("Welcome"); } }
	GUIStyle NoteStyle { get { return GUI.skin.GetStyle("Note"); } }
	GUIStyle TagButtonStyle { get { return GUI.skin.GetStyle("TagButton"); } }
	int PointsReceived { get { return ComputePoints(probOfCorrectAnswer); } }
	bool FirstTutorialQuestion { get { return !tutorialAnsweredOneQuestion && !tutorialShowScore ; } }
	bool SecondTutorialQuestion { get { return tutorialAnsweredOneQuestion && !tutorialShowScore ; } }
	public int QuestionCount { get { return this.questionCount; } set { this.questionCount = value; } }
	public int CurrentQuestionIndex { get { return this.currentQuestion; } }
	public Question CurrentQuestion { get { return QuestionsScript.singleton.GetQuestion(this.currentQuestion); } }
	#endregion
	
	
	
	void Awake(){
		singleton = this;
		reverseOptions = Random.value >= 0.5f;
		
		if(!Application.isWebPlayer){
			if(File.Exists(SaveGameFilename)){
				LoadGame();
			} else {
				SaveGame();
			}
		}
		//SetTutorialStatus(false);

		if(tutorialFinished) gameStep = GameStep.Question;
		Application.targetFrameRate = 60;
	}
	
	void SaveGame(){
		if(Application.isWebPlayer) return;
		StartCoroutine_Auto(SaveGameCoroutine());
	}
	
	bool saving = false;
	IEnumerator SaveGameCoroutine(){
		while(saving) yield return new WaitForSeconds(.1f);
		saving = true;
		StreamWriter writer = new StreamWriter(SaveGameFilename);
		writer.WriteLine(tutorialFinished);
		writer.WriteLine(totalScore);
		writer.WriteLine(averageScore);
		writer.WriteLine(answers.Count);
		for(int n = 0; n < answers.Count; n++){
			writer.WriteLine(answers[n].actualPercent);
			writer.WriteLine(answers[n].percentConfidence);
			writer.WriteLine(answers[n].score);
		}
		writer.WriteLine(QuestionsScript.tags.Count);
		foreach(KeyValuePair<string, TagUsage> pair in QuestionsScript.tags){
			writer.WriteLine(pair.Key);
			writer.WriteLine(pair.Value.ToString());
		}
		writer.Close();
		saving = false;
		yield return new WaitForSeconds(0f);
	}
	
	void LoadGame(){
		if(Application.isWebPlayer) return;
		StreamReader reader = new StreamReader(SaveGameFilename);
		SetTutorialStatus(bool.Parse(reader.ReadLine()));
		totalScore = double.Parse(reader.ReadLine());
		averageScore = double.Parse(reader.ReadLine());
		int answersCount = int.Parse(reader.ReadLine());
		for(int n = 0; n < answersCount; n++){
			Answer answer = new Answer();
			answer.actualPercent = double.Parse(reader.ReadLine());
			answer.percentConfidence = double.Parse(reader.ReadLine());
			answer.score = int.Parse(reader.ReadLine());
			answers.Add(answer);
		}
		string tagCountStr = reader.ReadLine();
		if(tagCountStr != null){
			int tagCount = int.Parse(tagCountStr);
			for(int n = 0; n < tagCount; n++){
				string tagName = reader.ReadLine();
				if(QuestionsScript.tags.ContainsKey(tagName)){
					try {
						QuestionsScript.tags[tagName] = (TagUsage)System.Enum.Parse(typeof(TagUsage), reader.ReadLine());
					} catch(System.ArgumentException e){
						Debug.LogError(e);
					}
				}
			}
		}
		reader.Close();
	}
	
	void SetTutorialStatus(bool finished){
		tutorialFinished = finished;
		tutorialAnsweredOneQuestion = false;
		tutorialShowScore = finished;
	}
	
	int ComputePoints(double probOfCorrectAnswer){
		return (int)System.Math.Round(100.0 * (System.Math.Log(probOfCorrectAnswer / 100.0, 2.0) + 1.0));
	}
	
	public void OnPostRender() {
		if(gameStep == GameStep.Graph){
			Graph.DrawAnswers(bars, material);
		}
    }
	
	void Update(){
		if(gameStep == GameStep.DownloadingDatabases && QuestionDatabase.justLoadedDatabases){
			QuestionDatabase.justLoadedDatabases = false;
			gameStep = GameStep.QuestionDatabases;
		}
		if(gameStep == GameStep.AddingNewDatabase && QuestionDatabase.justLoadedDatabases){
			QuestionDatabase.justLoadedDatabases = false;
			gameStep = GameStep.QuestionDatabases;
		}
		if (updatedStep != gameStep)
		{
			updatedStep = gameStep;
			nextHighlightedKey = "";
		}
		highlightedKey = nextHighlightedKey;
	}
	
	void OnGUI(){
		GUI.skin = defaultSkin;

		if(TouchScreen){
			Input.multiTouchEnabled = false;
			GUI.skin.customStyles[4].hover = GUI.skin.customStyles[4].normal; // <-- wait, I think this gets saved in the data!
		}
		else
		{
			// Listen for keyboard events:
			Event evt = Event.current;
			if ((evt.type == EventType.KeyDown) && (evt.keyCode != KeyCode.None)) {
				string key = evt.keyCode.ToString().ToLower();
				if (key.Length == 1) {
					if (nextHighlightedKey == key) {
						if (!waitingForUp)
						{
							nextHighlightedKey = "";
						}
					}
					else {
						nextHighlightedKey = key;
					}
					waitingForUp = true;
				}
			}
			else if (waitingForUp && (evt.type == EventType.KeyUp) && (evt.keyCode != KeyCode.None)) {
				string key = evt.keyCode.ToString().ToLower();
				if (key.Length == 1) {
					waitingForUp = false;
				}
			}
		}

		// Set highlighted style for keyboard shortcuts (inefficient)
		highlightedStyle = new GUIStyle(GUI.skin.customStyles[4]);
		highlightedStyle.normal = highlightedStyle.hover;
			
		if(restartingGame){
			RestartGameGUI();
		} else if(uiIntructionsPage >= 0){
			StartGameGUI();
		}
		else {
			switch(updatedStep)
			{
			case GameStep.Start:
				StartGameGUI();
				break;
			case GameStep.Question:
				GameQuestionGUI();
				break;
			case GameStep.Answer:
				GameAnswerGUI();
				break;
			case GameStep.Graph:
				GameGraphGUI();
				break;
			case GameStep.ExplainScores:
				GameExplainScoresGUI();
				break;
			case GameStep.EndTutorial:
				GameEndTutorialGUI();
				break;
			case GameStep.Options:
				OptionsGUI();
				break;
			case GameStep.QuestionTypes:
				QuestionTypesGUI();
				break;
			case GameStep.QuestionDatabases:
				QuestionDatabasesGUI();
				break;
			case GameStep.DownloadingDatabases:
				DownloadingDatabasesGUI();
				break;
			case GameStep.AddingNewDatabase:
				AddingNewDatabaseGUI();
				break;
			}
		}
	}
	
	void StartLayout(){
		float offsetX = 15f, offsetY = 15f;
		float screenWidth = Screen.width - offsetX * 2f, screenHeight = Screen.height - offsetY * 2f;
		GUILayout.BeginArea(new Rect(offsetX, offsetY, screenWidth, screenHeight));
		GUILayout.BeginVertical();
	}
	
	void EndLayout(){
		GUILayout.EndVertical();
		GUILayout.EndArea();
	}
	
	void ScoreLabel(string text, string scoreText, bool large){
		if(text.Length > 0) GUILayout.Label(text, GUI.skin.customStyles[3], GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
		GUILayout.Label(scoreText, GUI.skin.customStyles[large ? 0 : 1], GUILayout.ExpandWidth(false));
	}
	
	void SetGuiContentColorForScore(double score){
		GUI.contentColor = score == 0.0 ? Color.black : score < 0.0 ? new Color(0.3f, 0f, 0f) : new Color(0f, 0f, 0.4f);
	}
	
	void ShowScores(){
		GUILayout.BeginVertical("box", GUILayout.ExpandWidth(false));
		GUI.backgroundColor = Color.yellow;
		GUI.contentColor = Color.black;
		SetGuiContentColorForScore(averageScore);
		ScoreLabel("Average Score", averageScore.ToString("0.#"), true);
		if(!tutorialShowScore) GUILayout.FlexibleSpace();
		
		double lastFewSum = 0.0;
		if(answers.Count > 0){
			answers.GetRange(Mathf.Max(0, answers.Count - lastXScore), Mathf.Min(answers.Count, lastXScore)).ForEach((tempAnswer) => lastFewSum += tempAnswer.score);
			lastFewSum /= (double)Mathf.Min(answers.Count, lastXScore);
		}
		SetGuiContentColorForScore(lastFewSum);
		ScoreLabel("Last " + lastXScore + " Answers", lastFewSum.ToString("0.#"), true);
		if(!tutorialShowScore) GUILayout.FlexibleSpace();
		
		SetGuiContentColorForScore((int)totalScore);
		ScoreLabel("Total Score (" + answers.Count + " answers)", "" + (int)totalScore, false);
		GUI.contentColor = Color.white;
		GUI.backgroundColor = Color.white;
		GUILayout.EndVertical();
	}
	
	void GuiQuestionBox(string question){
		GUILayout.Box("QUESTION " + (questionCount + 1) + ": " + question, GUILayout.ExpandWidth(true));
	}
	
	void GuiAnswersBox(string correctAnswer, string wrongAnswer){
		string answerA = reverseOptions ? wrongAnswer : correctAnswer;
		string answerB = reverseOptions ? correctAnswer : wrongAnswer;
		GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
		GUI.skin.label.normal.textColor = answerAColor;
		GUILayout.Label("A) " + answerA);
		GUI.skin.label.normal.textColor = answerBColor;
		GUILayout.Label("B) " + answerB);
		GUI.skin.label.normal.textColor = Color.black;
		GUILayout.EndVertical();
	}
	
	bool HitValidateKey() {
		if (Event.current.type == EventType.KeyDown){
			KeyCode code = Event.current.keyCode;
			return (code == KeyCode.Return) || (code == KeyCode.KeypadEnter) || (code == KeyCode.Space);
		}
		return false;
	}
	
	void GuiAddButtonLine(string[] labelKeys, bool aLine, Color lowColor, Color highColor)
	{
		bool validated = HitValidateKey();
		GUILayout.BeginHorizontal();
		GUI.contentColor = highColor;
		GUILayout.Label(aLine ? "A" : "B", GUI.skin.customStyles[2], GUILayout.Width(75f));
		GUI.contentColor = Color.white;
		for(int i = answersPerLine; i >= 0; i--){
			string key = labelKeys[i];
			// Style
			GUIStyle style = GUI.skin.customStyles[4];
			if (highlightedKey == key){
				style = highlightedStyle;
			}
			
			// Actual layout
			if(!TouchScreen){
				GUILayout.Label(key + ":",  GUILayout.Width(labelWidths[i]));
			}
			int percent = percentages[i];
			GUI.backgroundColor = Color.Lerp(Color.white, highColor, (percent - 50f) / 50.0f);
			if(GUILayout.Button(percent + "%", style) || (validated && (highlightedKey == key))){
				GiveAnswer(percent, reverseOptions ^ aLine);
			}
		}
		GUI.backgroundColor = Color.white;
		GUILayout.EndHorizontal();
	}
	
	void GuiAnswerButtons(){
		// Add keyboard shortcuts explanation text
		if (highlightedKey != ""){
			GUILayout.Label("(press Space or Enter to validate)");
		}
		
		GUILayout.BeginVertical("box");
		
		// Buttons for answer A
		if(FirstTutorialQuestion) GUILayout.Label("If you think A is the correct answer, and you think the probability of you being right is 70%, then press the 70% button in the A row (top, cyan row).");
		GuiAddButtonLine(labelKeysA, true, answerBColor, answerAColor);
		
		// Buttons for answer B
		if(FirstTutorialQuestion) GUILayout.Label("If you don't have any idea what the correct answer is, but you think it's slightly more likely to be B, then press the 50% button in the B row (bottom, green row).");
		GuiAddButtonLine(labelKeysB, false, answerAColor, answerBColor);
		
		GUILayout.EndVertical();
	}
	
	void GameQuestionGUI(){
		StartLayout();
		
		GUILayout.BeginHorizontal();
		if(tutorialShowScore){
			ShowScores();
			GUILayout.Space(20f);
		}
		GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
		
		GuiQuestionBox(CurrentQuestion.m_questionText);
		GuiAnswersBox(CurrentQuestion.m_correctAnswerText, CurrentQuestion.m_wrongAnswerText);
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
		
		GUILayout.FlexibleSpace();
		
		GuiAnswerButtons();
		
		EndLayout();
	}
	
	/// <summary>
	/// The player picked an answer.
	/// </summary>
	/// <param name='probability'>Probability given by the player to answer A.</param>
	void GiveAnswer(double probability, bool correct){
		gaveCorrectAnswer = correct;
		probOfCorrectAnswer = gaveCorrectAnswer ? probability : 100.0 - probability;
		totalScore += PointsReceived;
		averageScore = (PointsReceived + averageScore * answers.Count) / (double)(answers.Count + 1);
		answers.Add(new Answer(probability, gaveCorrectAnswer, PointsReceived));
		SaveGame();
		gameStep = GameStep.Answer;
	}
	
	void GameAnswerGUI(){
		StartLayout();
		
		bool viewGraphButton = answers.Count >= minAnswersForGraph && answers.Count % answersBetweenGraph == 0;
		
		GUILayout.BeginHorizontal();
		if(tutorialShowScore){
			GUILayout.BeginVertical();
			ShowScores();
			if(tutorialFinished && !viewGraphButton){
				GUILayout.BeginVertical("box");
				ViewGraphButton(false, false);
				if(GUILayout.Button("OPTIONS")){
					gameStep = GameStep.Options;
				}
				GUILayout.EndVertical();
			}
			GUILayout.EndVertical();
			GUILayout.Space(20f);
		}
		
		GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
		GUILayout.Box("QUESTION " + (questionCount + 1) + ": " + CurrentQuestion.m_questionText, GUILayout.ExpandWidth(true));
		GUILayout.BeginHorizontal("box", GUILayout.ExpandWidth(true));
		if(probOfCorrectAnswer == 50.0){
			GUI.contentColor = Color.black;
			GUI.backgroundColor = gaveCorrectAnswer ? Color.blue : Color.red;
			ScoreLabel("", "0", true);
			GUI.contentColor = Color.white;
			GUI.backgroundColor = Color.white;
			GUILayout.Label("You were " + (gaveCorrectAnswer ? "correct" : "incorrect") + ", but since you answered 50%, you don't " + (gaveCorrectAnswer ? "receive" : "lose") + " any points.\n" + (gaveCorrectAnswer ? CurrentQuestion.m_correctResponse : CurrentQuestion.m_wrongResponse));
		} else if(!gaveCorrectAnswer){
			GUI.contentColor = Color.black;
			GUI.backgroundColor = Color.red;
			ScoreLabel("", "" + PointsReceived, true);
			GUI.contentColor = Color.white;
			GUI.backgroundColor = Color.white;
			GUILayout.Label("Incorrect. " + CurrentQuestion.m_wrongResponse);
			
		} else {
			GUI.contentColor = Color.black;
			GUI.backgroundColor = Color.blue;
			ScoreLabel("", "+" + PointsReceived, true);
			GUI.contentColor = Color.white;
			GUI.backgroundColor = Color.white;
			GUILayout.Label("Correct! " + CurrentQuestion.m_correctResponse);
		}
		
		GUILayout.EndHorizontal();
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
		
		if(!tutorialShowScore){
			GUILayout.Label("Points are awarded based on the following table. Note that you lose a lot of points if you are very confident in the wrong answer.");
			GuiScoringTable(true);
			GUILayout.Label("Remember, your goal is to get as many points as you can.");
		}
		
		GUILayout.FlexibleSpace();
		
		if(viewGraphButton){
			ViewGraphButton(true, HitValidateKey());
		} else {
			if (tutorialFinished || !SecondTutorialQuestion)
			{
				if(GUILayout.Button("NEXT QUESTION", GUILayout.ExpandWidth(true)) || HitValidateKey()){
					GoToNextQuestion();
				}
			} else {
				if(GUILayout.Button("OKAY, GET MORE POINTS. GOT IT.", GUILayout.ExpandWidth(true)) || HitValidateKey()){
					gameStep = GameStep.ExplainScores;
				}
			}
		}
		
		EndLayout();
	}
	
	void ViewGraphButton(bool expandButton, bool isKeyPressed){
		if(GUILayout.Button("VIEW GRAPH", GUILayout.ExpandWidth(expandButton)) || isKeyPressed){
			selectedBar = -1;
			bars = Bar.GetBars(answers);
			gameStep = GameStep.Graph;
		}
	}
	
	void GoToNextQuestion(){
		questionCount++;
		currentQuestion++;
		reverseOptions = Random.value >= 0.5f;
		tutorialAnsweredOneQuestion = true;
		gameStep = GameStep.Question;
		// Reinit keyboard shortcuts
		highlightedKey = "";
	}

	void DownloadingDatabasesGUI(){
		StartLayout();
		
		GUILayout.Label("QUESTION DATABASES", WelcomeStyle);
		GUILayout.Label("Download progress: " + (int)(QuestionDatabase.LoadingProgress * 100f) + "%");
		EndLayout();
	}
	
	bool deleteUnselectedConfirmation = false;
	void QuestionDatabasesGUI(){
		StartLayout();
		
		GUILayout.Label("QUESTION DATABASES", WelcomeStyle);
		
		uiScrollPosition = GUILayout.BeginScrollView(uiScrollPosition);
		if(GUILayout.Button("ADD NEW DATABASE")){
			QuestionDatabase.resultLog = "";
			deleteUnselectedConfirmation = false;
			gameStep = GameStep.AddingNewDatabase;
		}
		foreach(QuestionDatabase database in QuestionDatabase.databases){
			GUILayout.BeginHorizontal();
			GUI.enabled = database.downloaded;
			database.used = GUILayout.Toggle(database.used, database.name);
			GUI.enabled = true;
			GUILayout.Label("(" + database.url + ")", NoteStyle);
			GUILayout.EndHorizontal();
		}
		GUILayout.EndScrollView();
		
		GUILayout.Space(15f);
		
		if(deleteUnselectedConfirmation){
			GUILayout.Label("Are you sure you want to delete ALL unselected databases? You can't undo this.");
		} else if(QuestionDatabase.resultLog != null){
			GUILayout.Label(QuestionDatabase.resultLog, NoteStyle);
		}
		
		GUILayout.BeginHorizontal();
		
		if(GUILayout.Button("BACK")){
			deleteUnselectedConfirmation = false;
			currentQuestion = -1;
			QuestionDatabase.resultLog = null;
			QuestionDatabase.SaveDatabases();
			QuestionsScript.singleton.LoadAllQuestions();
			QuestionDatabase.SaveDatabases();//save again, in case some databases failed to load
			gameStep = GameStep.Options;
		}
		
		GUILayout.FlexibleSpace();
		
		if(GUILayout.Button("UPDATE ALL")){
			deleteUnselectedConfirmation = false;
			StartCoroutine_Auto(QuestionDatabase.LoadDatabasesFromUrls());
			gameStep = GameStep.DownloadingDatabases;
		}
		
		GUILayout.FlexibleSpace();
		
		if(GUILayout.Button("DELETE UNSELECTED")){
			if(deleteUnselectedConfirmation){
				QuestionDatabase.RemoveUnselectedDatabases();
			}
			deleteUnselectedConfirmation = !deleteUnselectedConfirmation;
		}
		
		GUILayout.EndHorizontal();

		EndLayout();
	}
	
	void AddingNewDatabaseGUI(){
		StartLayout();
		
		GUILayout.Label("NEW DATABASE", WelcomeStyle);
		GUILayout.Space(15f);
		GUILayout.Label("URL:");
		QuestionDatabase.newDatabaseUrl = GUILayout.TextField(QuestionDatabase.newDatabaseUrl, GUILayout.ExpandWidth(true));
		if(QuestionDatabase.loadingDatabases){
			GUILayout.Label("Progress: " + (int)(QuestionDatabase.AddingProgress * 100f) + "%");
		} else if(GUILayout.Button("ADD")){
			StartCoroutine(QuestionDatabase.TryToAddNewDatabase());
		}
		
		GUILayout.FlexibleSpace();
		
		GUILayout.Label(QuestionDatabase.resultLog, NoteStyle);
		GUI.enabled = !QuestionDatabase.loadingDatabases;
		if(GUILayout.Button("BACK")){
			gameStep = GameStep.QuestionDatabases;
		}
		GUI.enabled = true;
		
		EndLayout();
	}
	
	void QuestionTypesGUI(){
		StartLayout();
		
		GUILayout.Label("QUESTION TYPES", WelcomeStyle);
		
		uiScrollPosition = GUILayout.BeginScrollView(uiScrollPosition);
		bool tagsChanged = false;
		
		GUILayout.BeginHorizontal();
		GUILayout.Label("Set everything to:");
		if(GUILayout.Button("None", TagButtonStyle)){
			foreach(string tag in QuestionsScript.tags.Keys) QuestionsScript.tags[tag] = TagUsage.None;
			tagsChanged = true;
		}
		if(GUILayout.Button("Some", TagButtonStyle)){
			foreach(string tag in QuestionsScript.tags.Keys) QuestionsScript.tags[tag] = TagUsage.Some;
			tagsChanged = true;
		}
		if(GUILayout.Button("All", TagButtonStyle)){
			foreach(string tag in QuestionsScript.tags.Keys) QuestionsScript.tags[tag] = TagUsage.All;
			tagsChanged = true;
		}
		GUILayout.EndHorizontal();
		GUILayout.Label("(A question will be asked iff all its tags are set to Some or at least one of its tags is set to All.)", NoteStyle);
		
		foreach(string tag in QuestionsScript.tags.Keys){
			GUILayout.BeginHorizontal();
			bool hasSubTag = QuestionsScript.HasSubTag(tag);
			bool parentSetToAll = false;
			string tagName = tag;
			if(hasSubTag){
				GUILayout.Space(30f);
				tagName = QuestionsScript.GetChildTag(tag);
				parentSetToAll = QuestionsScript.tags[QuestionsScript.GetParentTag(tag)] == TagUsage.All;
			}
			if(parentSetToAll){
				GUILayout.Label("All");
			} else {
				TagUsage oldValue = QuestionsScript.tags[tag];
				if(GUILayout.Button(oldValue.ToString(), TagButtonStyle)){
					QuestionsScript.tags[tag] = (TagUsage)(((int)oldValue + 1) % (int)TagUsage.COUNT);
				}
				tagsChanged |= oldValue != QuestionsScript.tags[tag];
			}
			
			GUILayout.Label(tagName);
			GUILayout.EndHorizontal();
		}
		if(tagsChanged){
			QuestionsScript.singleton.UpdateQuestionGenerators();
		}
		GUILayout.EndScrollView();
		
		GUILayout.Space(15f);
		
		GUILayout.BeginHorizontal();
		if(GUILayout.Button("BACK")){
			SaveGame();
			currentQuestion = -1;
			QuestionsScript.singleton.RegenerateQuestions();
			gameStep = GameStep.Options;
		}
		GUILayout.Label("Data tables available: " + QuestionsScript.generators.FindAll(generator => generator.m_active).Count + "/" + QuestionsScript.generators.Count, GUILayout.ExpandHeight(true));
		GUILayout.EndHorizontal();

		EndLayout();
	}
	
	void OptionsGUI(){
		StartLayout();
		
		GUILayout.Label("OPTIONS", WelcomeStyle);
		if(GUILayout.Button("QUESTION DATABASES")){
			deleteUnselectedConfirmation = false;
			uiScrollPosition = Vector2.zero;
			QuestionsScript.singleton.StopGeneratingQuestions();
			gameStep = GameStep.QuestionDatabases;
		}
		if(GUILayout.Button("QUESTION TYPES")){
			uiScrollPosition = Vector2.zero;
			gameStep = GameStep.QuestionTypes;
		}
		if(GUILayout.Button("REDO TUTORIAL")){
			GoToNextQuestion();
			SetTutorialStatus(false);
			uiIntructionsPage = -1;
			gameStep = GameStep.Start;
		}
		if(GUILayout.Button("GAME INFO")){
			uiScrollPosition = Vector2.zero;
			uiShowAllInstructions = true;
			uiIntructionsPage = 0;
		}
		GUI.color = new Color(1f, 0.5f, 0.5f);
		if(GUILayout.Button("RESTART GAME")){
			restartingGame = true;
		}
		GUI.color = Color.white;
		
		GUILayout.FlexibleSpace();
		
		if(GUILayout.Button("BACK")){
			GoToNextQuestion();
		}
		
		EndLayout();
	}
	
	void GameGraphGUI(){
		int newlySelectedBar = Graph.DrawAxisLabels(bars);
		if(newlySelectedBar >= 0){
			selectedBar = newlySelectedBar;
			uiScrollPosition = Vector2.zero;
		}
		
		StartLayout();
		
		GUILayout.BeginHorizontal();
		ShowScores();
		GUILayout.Space(30f);
		if(Event.current.type == EventType.Repaint){
			Graph.OffsetX = GUILayoutUtility.GetLastRect().xMax / Screen.width;
		}
		GUILayout.EndHorizontal();
		
		GUILayout.FlexibleSpace();
		GUILayout.Space(10f);
		if(Event.current.type == EventType.Repaint){
			Graph.Height = GUILayoutUtility.GetLastRect().yMax / Screen.height;
		}
		
		if(selectedBar >= 0){
			Bar bar = bars[selectedBar];
			uiScrollPosition = GUILayout.BeginScrollView(uiScrollPosition);
			if(tutorialFinished){
				GUILayout.Label("Answers: " + bar.GetListOfConfidenceValues());
				GUILayout.Label("Average credence: " + bar.x.ToString("G3") + "%");
				GUILayout.Label("Actual success rate: " + bar.y.ToString("G3") + "%");
				GUILayout.Label("Average score: " + bar.scoreAverage);
				GUILayout.Label("Total points: " + bar.scoreSum);
			} else {
				GUILayout.Label("This bar represents your average credence and success rate on the " + bar.Count + " question" + (bar.Count > 1 ? "s" : "") + " where you answered " + bar.GetAnswerRange() + " " + bar.GetListOfConfidenceValues() + ". On those questions:");
				GUILayout.Label(bar.x.ToString("G3") + "% was your average credence, ");
				GUILayout.Label(bar.y.ToString("G3") + "% was your actual success rate, and ");
				GUILayout.Label(bar.scoreAverage + " was your average score. (Those answers are responsible for " + bar.scoreSum + " points.)");
				if(System.Math.Abs(bar.x - bar.y) <= Answer.CLOSE_ENOUGH){
					GUILayout.Label("You seem to have been pretty well calibrated on those questions.  Well done!  Your average feeling of credence and actual success rate were fairly close.");
				} else if(bar.x < bar.y){
					GUI.color = Color.cyan;
					GUILayout.Label("You can probably improve this score, because you seem to have been under-confident on those questions. The arrow means that when you feel like answering " + bar.GetAnswerRange() + ", on average you can score more points if you adjust your credence upward by around " + bar.Adjustment.ToString("G3") + "%.");
				} else {
					GUI.color = Color.magenta;
					GUILayout.Label("You can probably improve this score, because you seem to have been over-confident on those questions. The arrow means that when you feel like answering " + bar.GetAnswerRange() + ", on average you can score more points if you adjust your credence downward by around " + bar.Adjustment.ToString("G3") + "%.");
				}
				GUILayout.Label("The yellow line shows the optimal bar height, i.e. the optimal success rate, for each credence level. Ideally, all your bars would be no higher or lower than this yellow line.");
			}
			GUILayout.EndScrollView();
			GUI.color = Color.white;
		} else {
			if(!tutorialFinished){
				GUILayout.Label("This graph will help you adjust your credence feeling toward your actual success rate, which will help you get more points.");
				GUILayout.Label(Click + " on the blue graph bar to see more information.");
			} else {
				GUILayout.Label(Click + " on a graph bar to see more information.");
			}
		}
		
		if(GUILayout.Button(tutorialFinished ? "CONTINUE" : "END TUTORIAL", GUILayout.ExpandWidth(true)) || HitValidateKey ()){
			GoToNextQuestion();
			if(!tutorialFinished){
				gameStep = GameStep.EndTutorial;
			}
		}
		EndLayout();
	}
	
	void StartGameGUI(){
		StartLayout();
		
		if(!tutorialFinished && uiIntructionsPage < 0){
			if(uiIntructionsPage == -2){
				GUILayout.FlexibleSpace();
				GUIEx.Centered(() => GUILayout.Label(cfarTexture, WelcomeStyle));
				GUILayout.Space(20f);
				GUIEx.Centered(() => GUILayout.Label("Welcome to the Credence Game!", WelcomeStyle));
				GUIEx.Centered(() => GUILayout.Label("Send bugs and comments to alexei@appliedrationality.org", NoteStyle));
				GUILayout.FlexibleSpace();
				GUIEx.Centered(() => {
					if(GUILayout.Button("LET'S START WITH A TUTORIAL!")){
						uiIntructionsPage++;
					}
				});
				GUIEx.Centered(() => {
					GUI.color = new Color(1f, 0.5f, 0.5f);
					if(GUILayout.Button("SKIP TUTORIAL")){
						SetTutorialStatus(true);
						SaveGame();
						gameStep = GameStep.Question;
					}
					GUI.color = Color.white;
				});
				GUILayout.FlexibleSpace();
			} else if(uiIntructionsPage == -1){
				GUILayout.Label("In this game you will be asked a series of questions. For example:");
				GuiQuestionBox(CurrentQuestion.m_questionText);
				GUILayout.Label("Each answer will have two options:");
				GuiAnswersBox(CurrentQuestion.m_correctAnswerText, CurrentQuestion.m_wrongAnswerText);
				GUILayout.Label("You'll have to pick what answer you think is correct. Moreover, you'll have to specify how confident you are in your answer.");
				GUILayout.Label("If you answer the question correctly, you will gain points. If you answer the question incorrectly, you will lose points.");
				GUILayout.Label("Your goal is to get as many points as you can.");
				
				GUILayout.FlexibleSpace();
				
				if(GUILayout.Button("LET'S DO THIS!", GUILayout.ExpandWidth(true))){
					gameStep = GameStep.Question;
				}
			}
		} else if(uiIntructionsPage >= 0){
			uiScrollPosition = GUILayout.BeginScrollView(uiScrollPosition);
			if(uiIntructionsPage == 0){
				GUILayout.Label("Welcome to the Credence Game, where information is the goal and credence calibration is how you improve!");
		        GUILayout.Label("What happens: You answer a series of two-choice questions, and indicate a credence level between 50% and 99% for each answer.");
		        GUILayout.Label("Your objective: To earn something called an 'information score', measured in 'centibits', which are simply points awarded by the following rule:");
		        
				GuiScoringTable(false);
				
		        GUILayout.Label("The optimal strategy: on each question, sincerely indicate how confident you are in your answer!");
				GUILayout.Label("How to improve: By calibrating your credence level!  We will regularly give you feedback about whether you are overconfident or underconfident in your answers, and adjusting your credence levels accordingly will improve your average score.");
				
				GUILayout.Label("Don't forget, the goal of the game is to earn points.  The calibration statistics are just feedback to help you do better.  If calibration were your only goal, you could just click 50% all the time and be perfectly calibrated!  But then you wouldn't be any more informative than a coin flip.  That's why the goal is to earn points, and we carefuly chose our scoring rule so that calibration would be your best way of doing that (trust us!).");
			} else if(uiIntructionsPage == 1) {
				GUILayout.Label("Score = round(100 * (log2(percent_probability_of_correct_answer / 100) + 1))");
				GuiScoringTable(false);
				//Note: you can think of it in terms of surprise or adjustment you'll have to make
				GUILayout.Label("Why these numbers?  Your score is the amount of information you've provided, in centibits, or hundreths of a bit.  Giving a correct, 100% confident answer to a two-choice question is providing 1 bit of information, or 100 centibits.  Giving an 80% confident right answer is giving 68 centibits of information because 80% = 2^(0.68)*50%, i.e. you are doing 2^(0.68) times better than a coin flip.  Giving an 80% confident wrong answer is giving -132 centibits of information because 20% = 2^(-1.32)*50%, i.e. you are doing 2^(1.32) times worse than a coin flip.");
				GUILayout.Label("\nIt's a theorem that giving points in this way makes giving your sincere credence levels the best strategy to maximize your expected score, and that calibrating your credence levels to your actual success rates will improve your performance!");
				//GUILayout.Label("If you would like to understand why the numbers above are really the exact, information-theoretic amounts of information each credence level provides, check out our web tutorial on [information scoring](link).");
			} else {
				GUILayout.Label("CREDITS");
				GUILayout.Label("\nProgramming:");
				GUILayout.Label("(Send bugs and comments to this guy.)");
				GUILayout.Label("Alexei Andreev (alexei@appliedrationality.com)");
				GUILayout.Label("See: appliedrationality.org");
				GUILayout.Label("\nDesign:");
				GUILayout.Label("Andrew Critch (critch@math.berkeley.edu)");
				GUILayout.Label("See: math.berkeley.edu/~critch/");
				GUILayout.Label("\nQuestions database (created from Wikipedia pages):");
				GUILayout.Label("Andrew Critch and Zachary Aletheia");
			}
			GUILayout.EndScrollView();
			
			GUILayout.FlexibleSpace();        
			
			bool backToGame = !uiShowAllInstructions || uiIntructionsPage == 2;
			if(GUILayout.Button(backToGame ? "BACK TO THE GAME" : (uiIntructionsPage == 0 ? "EXPLAIN SCORING SYSTEM" : "CREDITS"), GUILayout.ExpandWidth(true))){
				if(backToGame){
					uiShowAllInstructions = false;
					uiIntructionsPage = -2;
				} else {
					uiScrollPosition = Vector2.zero;
					uiIntructionsPage++;
				}
			}
		}
		
		EndLayout();
	}
	
	void GameExplainScoresGUI(){
		StartLayout();
		
		GUILayout.BeginHorizontal();
		ShowScores();
		
		GUILayout.BeginVertical();
		GUILayout.Label("This is your mean score across all the questions you've answered so far.");
		GUILayout.FlexibleSpace();
		GUILayout.Label("This is your mean score across the last " + lastXScore + " questions you've answered.");
		GUILayout.FlexibleSpace();
		GUILayout.Label("This is the total number of points you've accumulated over all the questions you've answered so far. Try to get as many points as you can!");
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
		
		GUILayout.FlexibleSpace();
		if(GUILayout.Button("NEXT QUESTION", GUILayout.ExpandWidth(true)) || HitValidateKey()){
			tutorialShowScore = true;
			GoToNextQuestion();
		}
		EndLayout();
	}
	
	void GameEndTutorialGUI(){
		StartLayout();
		
		GUILayout.Label("This is it! Remember, here is how you are scored on your answers:");
		GuiScoringTable(true);
		GUILayout.Label("The best strategy is to sincerely indicate how confident you are in your answer, and then adjust based on the graph the game will show you!");
		GUILayout.Label("\nGood luck and have fun!");
		
		GUILayout.FlexibleSpace();
		GUILayout.BeginHorizontal();
		if(GUILayout.Button("NEXT QUESTION", GUILayout.ExpandWidth(true)) || HitValidateKey()){
			SetTutorialStatus(true);
			SaveGame();
			gameStep = GameStep.Question;
		}
		GUI.color = new Color(1f, 0.5f, 0.5f);
		if(GUILayout.Button("MORE DETAILS")){
			SetTutorialStatus(true);
			SaveGame();
			gameStep = GameStep.Question;
			uiIntructionsPage = 0;
			uiShowAllInstructions = true;
			uiScrollPosition = Vector2.zero;
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
		EndLayout();
	}
	
	void RestartGameGUI(){
		StartLayout();
		
		GUILayout.Label("Do you want to restart the game? You'll lose all your progress and score.");
		GUILayout.FlexibleSpace();
		GUILayout.BeginHorizontal();
		if(GUILayout.Button("GO BACK", GUILayout.ExpandWidth(true))){
			restartingGame = false;
		}
		GUI.color = new Color(1f, 0.5f, 0.5f);
		if(GUILayout.Button("RESTART")){
			File.Delete(SaveGameFilename);
			Application.LoadLevel(Application.loadedLevel);
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();
		
		EndLayout();
	}
	
	void GuiScoringTable(bool showQuestionButton){
		// Scoring rules table.
		GUILayout.BeginHorizontal("box");
		// Header column.
		GUILayout.BeginVertical();
		GUILayout.Label("Credence");
		GUILayout.Label("Score (if correct)");
		GUILayout.Label("Score (if wrong)");
		GUILayout.EndVertical();
		
		// Percent columns.
		for(double p = 50.0; p <= 100.0; p += 10.0){
			if(p == 100.0) p = 99.0;
			GUILayout.BeginVertical();
			GUILayout.Label(p + "%");
			GUILayout.Label((p == 50.0 ? "" : "+") + ComputePoints(p));
			GUILayout.Label("" + ComputePoints(100.0 - p));
			GUILayout.EndVertical();
		}
		if(showQuestionButton){
			if(GUILayout.Button("?")){
				uiScrollPosition = Vector2.zero;
				uiIntructionsPage = 1;
			}
		}
		GUILayout.EndHorizontal();
	}
}
