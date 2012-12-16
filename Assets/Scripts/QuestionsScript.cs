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
	
	private int currentQuestionIndex = - 1;
	private List<Question> questions;
	private bool loadedDatabases = false;
	private bool loadedQuestions = false;
	private bool generatingQuestions = false;
	
	// Use this for initialization
	void Awake () {
		singleton = this;
		StartCoroutine_Auto(LoadAllQuestions());
	}
	
	IEnumerator LoadAllQuestions(){
		QuestionDatabase.databases = QuestionDatabase.LoadDatabases();
		loadedDatabases = true;
		foreach(QuestionDatabase database in QuestionDatabase.databases){
			if(!database.used) continue;
			LoadQuestions(database);
		}
		
		tags = new SortedDictionary<string, TagUsage>();
		foreach(QuestionGenerator generator in generators){
			foreach(string tag in generator.m_tags){
				tags[tag] = TagUsage.Some;
				if(HasSubTag(tag)) tags[GetParentTag(tag)] = TagUsage.Some;
			}
		}
		
		RegenerateQuestions();
		Instantiate(gamePrefab);
		loadedQuestions = true;
		
		yield return null;
	}
	
	public Question GetQuestion(int questionIndex){
		currentQuestionIndex = questionIndex;
		return questions[currentQuestionIndex];
	}
	
	public void LoadQuestions(QuestionDatabase database){
		generators = new List<QuestionGenerator>();
		string path = Path.Combine(Application.streamingAssetsPath, database.fileIndex + ".xml");
		using(XmlReader reader = XmlReader.Create(path)){
			reader.ReadToDescendant("QuestionGenerator");
			do{
				QuestionGenerator generator = QuestionGenerator.CreateGeneratorFromXml(reader);
				generator.m_database = database;
				if(generator != null) generators.Add(generator);
			}while(reader.ReadToNextSibling("QuestionGenerator"));
		}
	}
	
	public void RegenerateQuestions(){
		questions = new List<Question>();
		if(!generatingQuestions){
			StartCoroutine_Auto(GenerateQuestions());
			generatingQuestions = true;
		}
		while(questions.Count < 10) {}
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
			while(questions.Count - currentQuestionIndex > 20) yield return new WaitForSeconds(5);
			RandomizeGenerators();
			foreach(QuestionGenerator generator in generators){
				if(!generator.m_active || !generator.m_database.used) continue;
				if(Random.value > generator.m_weight) continue;
				questions.Add(generator.GenerateQuestion());
			}
		}
    }
	
	private void RandomizeGenerators(){
		for(int n = 0; n < 100 * generators.Count; n++){
			int index = Random.Range(0, generators.Count);
			QuestionGenerator q = generators[index];
			generators.RemoveAt(index);
			generators.Add(q);
		}
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
	
	/*public class QuestionTag {
		
		public string m_tagName;
		public bool m_active = true;
		public List<KeyValuePair<string, bool>> m_subTags = new List<KeyValuePair<string, bool>>();
		
		
		public QuestionTag(string tagName){
			m_tagName = tagName;
			int subtagIndex = m_tagName.IndexOf(SUBTAG_DELIMITER);
			if(subtagIndex >= 0){
				m_subTags.Add(m_tagName.Substring(subtagIndex + SUBTAG_DELIMITER.Length));
				m_tagName = m_tagName.Substring(0, subtagIndex);
			}
		}
		
		public void AddTag(string tagName){
			int subtagIndex = m_tagName.IndexOf(SUBTAG_DELIMITER);
			if(subtagIndex >= 0){
				string subTag = m_tagName.Substring(subtagIndex + SUBTAG_DELIMITER.Length);
				m_subTags.Remove(subTag);
				m_subTags.Add(subTag);
			}
		}
		
		/*public override bool Equals(object obj)
		{
			QuestionTag other = obj as QuestionTag;
			if(other == null) return base.Equals(obj);
			if(!m_tagName.Equals(other.m_tagName)) return false;
			if(m_subTag == null) return true;
			return m_subTag.Equals(other.m_subTag);
		}*/
		/*
		public override int GetHashCode ()
		{
			return m_tagName.GetHashCode();
		}
	}*/
}
