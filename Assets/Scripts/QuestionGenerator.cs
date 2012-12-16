using UnityEngine;
using System.Xml;
using System.Collections;
using System.Collections.Generic;

public class QuestionGenerator {
	
	public enum GeneratorType { Sorted, Match, SortedMultiset };
	
	public QuestionDatabase m_database;
	public GeneratorType m_type;
	public string[] m_tags;
	public string m_questionText = "";
	public string m_infoPrefix = "";
	public string m_infoSuffix = "";
	public int m_adjacentWithin = -1;
	public float m_weight = 1f;
	public bool m_active = true;
	protected List<QuestionAnswer> m_answers = new List<QuestionAnswer>();
	
	public int AnswersCount { get { return m_answers.Count; } }
	
	public Question GenerateQuestion(){
		if(m_type == GeneratorType.Match){
			return GenerateReplaceQuestion();
		} else {
			return GenerateSortedQuestion();
		}
	}
	
	public static QuestionGenerator CreateGeneratorFromXml(XmlReader reader){
		bool used = reader.GetAttribute("Used").Equals("y");
		if(!used) return null;
		QuestionGenerator generator = new QuestionGenerator();
		generator.m_tags = reader.GetAttribute("Tags").Split(';');
		generator.m_type = (GeneratorType)System.Enum.Parse(typeof(GeneratorType), reader.GetAttribute("Type"));
		generator.m_weight = System.Convert.ToSingle(reader.GetAttribute("Weight"));
		generator.m_questionText = reader.GetAttribute("QuestionText");
		generator.m_adjacentWithin = System.Convert.ToInt32(reader.GetAttribute("AdjacentWithin"));
		generator.m_infoPrefix = reader.GetAttribute("InfoPrefix");
		generator.m_infoSuffix = reader.GetAttribute("InfoSuffix");
		bool preventDuplicates = generator.m_type == GeneratorType.SortedMultiset;
		
		reader.ReadToDescendant("Answer");
		do{
			generator.AddAnswer(reader.GetAttribute("Text"), reader.GetAttribute("Value"), preventDuplicates);
		}while(reader.ReadToNextSibling("Answer"));
		return generator;
	}
	
	private Question GenerateReplaceQuestion(){
		int correctIndex, wrongIndex;
		do {
			correctIndex = Random.Range(0, m_answers.Count);
			if(m_adjacentWithin > 0){
				wrongIndex = Random.Range(Mathf.Max(0, correctIndex - m_adjacentWithin), Mathf.Min(m_answers.Count, correctIndex + m_adjacentWithin));
			} else {
				wrongIndex = Random.Range(0, m_answers.Count);
			}
		} while(correctIndex == wrongIndex);

		Question question = new Question();
		question.m_questionText = m_questionText.Replace("__", m_answers[correctIndex].m_text);
		question.m_correctAnswerText = m_answers[correctIndex].m_value;
		question.m_wrongAnswerText = m_answers[wrongIndex].m_value;
		question.m_correctResponse = "The answer is " + question.m_correctAnswerText + ".";
		question.m_wrongResponse = "The right answer is " + question.m_correctAnswerText + ", not " + question.m_wrongAnswerText + ".";
		return question;
	}
	
	private Question GenerateSortedQuestion(){
		int index1, index2;
		do {
			index1 = Random.Range(0, m_answers.Count);
			index2 = Random.Range(0, m_answers.Count);
		} while(m_answers[index1].m_value.Equals(m_answers[index2].m_value));
		int correctIndex = Mathf.Min(index1, index2);
		int wrongIndex = Mathf.Max(index1, index2);
		
		Question question = new Question();
		question.m_questionText = m_questionText;
		question.m_correctAnswerText = m_answers[correctIndex].m_text;
		question.m_wrongAnswerText = m_answers[wrongIndex].m_text;
		question.m_correctResponse = "The answer is " + question.m_correctAnswerText + " (" + m_infoPrefix + m_answers[correctIndex].m_value + m_infoSuffix + ") vs " + question.m_wrongAnswerText + " (" + m_infoPrefix + m_answers[wrongIndex].m_value + m_infoSuffix + ").";
		question.m_wrongResponse = "The right answer is " + question.m_correctAnswerText + " (" + m_infoPrefix + m_answers[correctIndex].m_value + m_infoSuffix + ") vs " + question.m_wrongAnswerText + " (" + m_infoPrefix + m_answers[wrongIndex].m_value + m_infoSuffix + ").";
		return question;
	}
	
	public void AddAnswer(string text, string value, bool preventDuplicates){
		text = text.Trim();
		value = value.Trim();
		int valueInt;
		if(int.TryParse(value, out valueInt)){
			if(valueInt > 9999) value = valueInt.ToString("N");
		}
		if(text.Length <= 0 || value.Length <= 0) return;
		if(preventDuplicates && m_answers.Exists((_answer) => _answer.m_text.Equals(text))) return;
		QuestionAnswer answer = new QuestionAnswer();
		answer.m_text = text;
		answer.m_value = value;
		m_answers.Add(answer);
	}
	
	protected class QuestionAnswer {
		public string m_text;
		public string m_value;
	}
}