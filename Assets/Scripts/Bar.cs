using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Bar {
	
	private static int m_answersPerBucket = 5;
	
	public List<Answer> answers = new List<Answer>();
	public double x, y;
	public double minX, maxX;
	public int scoreSum, scoreAverage;
	
	// Properties.
	public int Count { get { return answers.Count; } }
	public double Adjustment { get { return System.Math.Abs(x - y); } }
	
	public void AddRange(List<Answer> addedAnswers){
		answers.AddRange(addedAnswers);
	}
	
	public void ComputeCoordinates(){
		minX = double.MaxValue;
		maxX = double.MinValue;
		x = y = 0.0;
		scoreSum = 0;
		foreach(Answer answer in answers){
			minX = System.Math.Min(minX, answer.percentConfidence);
			maxX = System.Math.Max(maxX, answer.percentConfidence);
			y += answer.actualPercent;
			x += answer.percentConfidence;
			scoreSum += answer.score;
		}
		x /= Count;
		y /= Count;
		scoreAverage = Mathf.RoundToInt(scoreSum / (float)Count);
	}
	
	public string GetAnswerRange(){
		SortedDictionary<double, int> values = new SortedDictionary<double, int>();
		foreach(Answer answer in answers){
			if(!values.ContainsKey(answer.percentConfidence)){
				values[answer.percentConfidence] = 1;
			} else {
				values[answer.percentConfidence]++;
			}
		}
		
		string list = "";
		int index = 0;
		foreach(KeyValuePair<double, int> pair in values){
			list += pair.Key.ToString("G2") + "%";
			if(index == values.Count - 2) list += " or ";
			else if(index < values.Count - 2) list += ", ";
			index++;
		}
		return list + "";
	}
	
	public string GetListOfConfidenceValues(){
		SortedDictionary<double, int> values = new SortedDictionary<double, int>();
		foreach(Answer answer in answers){
			if(!values.ContainsKey(answer.percentConfidence)){
				values[answer.percentConfidence] = 1;
			} else {
				values[answer.percentConfidence]++;
			}
		}
		
		string list = "{";
		bool addComma = false;
		foreach(KeyValuePair<double, int> pair in values){
			if(addComma) list += ", ";
			list += pair.Key.ToString("G2") + "% x " + pair.Value;
			addComma = true;
		}
		return list + "}";
	}

	public static List<Bar> GetBars(List<Answer> answers, int maxAnswers){
		// only get the last 100 answers
		List<Answer> recentAnswers;
		if ((answers.Count > maxAnswers) && (maxAnswers > 0)) {
			recentAnswers = answers.GetRange(answers.Count - maxAnswers, maxAnswers);
		} else {
			recentAnswers = answers;
		}
		// Sort answers into buckets
		List<Bar> answerBars = new List<Bar>();
		int bucketIndex = 0;
		for(double p = 100.0; p >= 50.0; p -= 10.0){
			if(answerBars.Count <= bucketIndex){
				answerBars.Add(new Bar());
			}
			answerBars[bucketIndex].AddRange(recentAnswers.FindAll((tempAnswer) => tempAnswer.percentConfidence == (p == 100.0 ? 99.0 : p)));
			if(answerBars[bucketIndex].Count >= m_answersPerBucket) bucketIndex++;
		}
		int buckets = answerBars.Count;
		if(buckets > 1 && answerBars[buckets - 1].Count < m_answersPerBucket){
			answerBars[buckets - 2].AddRange(answerBars[buckets - 1].answers);
			answerBars.RemoveAt(buckets - 1);
		}
		
		foreach(Bar bar in answerBars){
			bar.ComputeCoordinates();
		}
		return answerBars;
	}
}
