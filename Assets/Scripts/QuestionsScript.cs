using UnityEngine;
using System.IO;
using System.Xml;
using System.Collections;
using System.Collections.Generic;

public class QuestionsScript : MonoBehaviour {
	
	public enum TagUsage { None, Some, All, COUNT };
	
	const string SUBTAG_DELIMITER = "::";
	public static QuestionsScript singleton;
	public static List<QuestionGenerator> generators;
	public static SortedDictionary<string, TagUsage> tags;
	
	public GameObject gamePrefab;
	
	private List<Question> questions;
	private List<QuestionGenerator>.Enumerator qEnum;
	private static bool qEnumValid = false;
	private bool loadedDatabases = false;
	private bool loadedQuestions = false;
	
	// Use this for initialization
	void Awake () {
		singleton = this;
		StartCoroutine(LoadAllQuestionsCoroutine());
	}
	
	IEnumerator LoadAllQuestionsCoroutine(){
		LoadAllQuestions();
		yield break;
	}
	
	public void LoadAllQuestions(){
		generators = new List<QuestionGenerator>();
		tags = new SortedDictionary<string, TagUsage>();
		QuestionDatabase.databases = QuestionDatabase.LoadDatabases();
		loadedDatabases = true;
		
		bool existsGoodDatabase = false;
		foreach(QuestionDatabase database in QuestionDatabase.databases){
			LoadQuestions(database);
			existsGoodDatabase |= database.downloaded && database.used;
		}
		if(!existsGoodDatabase){
			foreach(QuestionDatabase database in QuestionDatabase.databases){
				if(database.downloaded){
					Debug.LogWarning("Since no good database has been found, " + database.name + " has been enabled.");
					database.used = true;
					break;
				}
			}
		}
		
		RegenerateQuestions();
		StartCoroutine(GenerateQuestions());
		if(!loadedQuestions) Instantiate(gamePrefab);
		loadedQuestions = true;
	}
	
	public void StopGeneratingQuestions(){
		StopCoroutine("GenerateQuestions");
	}
	
	public Question GetQuestion(int questionIndex){
		return questions[questionIndex];
	}
	
	public static void LoadQuestions(QuestionDatabase database){
		try {
			using(XmlReader reader = XmlReader.Create(database.FilePath)){
				reader.ReadToDescendant("QuestionGenerator");
				do {
					QuestionGenerator generator = QuestionGenerator.CreateGeneratorFromXml(reader);
					generator.m_database = database;
					if(generator != null){
						qEnumValid = false;
						if(database.used) generators.Add(generator);
						foreach(string tag in generator.m_tags){
							tags[tag] = TagUsage.Some;
							if(HasSubTag(tag)) tags[GetParentTag(tag)] = TagUsage.Some;
						}
					}
				} while(reader.ReadToNextSibling("QuestionGenerator"));
			}
		} catch {
			database.used = false;
			database.downloaded = false;
		}
	}
	
	public void RegenerateQuestions(){
		questions = new List<Question>();
		while(questions.Count < 2){
			GenerateQuestionsStep();
		}
	}
	
	public void UpdateQuestionGenerators(){
		foreach(QuestionGenerator generator in generators){
			bool active = true;
			foreach(string tag in generator.m_tags){
				if(HasSubTag(tag) && tags[GetParentTag(tag)] == TagUsage.All){
					active = true;
					break;
				}
				if(tags[tag] == TagUsage.All){
					active = true;
					break;
				} else if(tags[tag] == TagUsage.None){
					active = false;
				}
			}
			generator.m_active = active;
		}
	}
	
	IEnumerator GenerateQuestions() {
		while(true){
			int currentQuestionIndex = GameScript.singleton == null ? 0 : GameScript.singleton.CurrentQuestionIndex;
			if(questions.Count - currentQuestionIndex <= 10){
				GenerateQuestionsStep();
			}
			yield return null;
		}
    }
	
	void GenerateQuestionsStep(){
		if(!qEnumValid){
			RandomizeGenerators();
			qEnum = generators.GetEnumerator();
			qEnumValid = true;
		}
		if(qEnum.MoveNext()){
			QuestionGenerator generator = qEnum.Current;
			if(generator.m_active && generator.m_database.used){
				if(Random.value <= generator.m_weight){
					questions.Add(generator.GenerateQuestion());
				}
			}
		} else {
			qEnumValid = false;
		}
	}
	
	private void RandomizeGenerators(){
		for(int n = 0; n < generators.Count - 1; n++){
			int index = Random.Range(n, generators.Count);
			QuestionGenerator old = generators[n];
			generators[n] = generators[index];
			generators[index] = old;
		}
		qEnumValid = false;
	}
	
	void OnGUI(){
		if(loadedQuestions) return;
		if(!loadedDatabases){
			GUI.Label(new Rect(15f, 15f, Screen.width, Screen.height), "Loading question databases...");
			return;
		}
		GUI.Label(new Rect(15f, 15f, Screen.width, Screen.height), "Found " + QuestionDatabase.databases.Count + " question databases.");
		GUI.Label(new Rect(15f, 15f, Screen.width, Screen.height), "Loading questions...");
	}
	
	public static bool HasSubTag(string tag){
		return tag.IndexOf(SUBTAG_DELIMITER) >= 0;
	}
	
	public static string GetParentTag(string tag){
		return tag.Substring(0, tag.IndexOf(SUBTAG_DELIMITER));
	}
	
	public static string GetChildTag(string tag){
		return tag.Substring(tag.IndexOf(SUBTAG_DELIMITER) + SUBTAG_DELIMITER.Length);
	}
}
