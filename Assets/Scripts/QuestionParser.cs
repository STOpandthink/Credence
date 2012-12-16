using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class QuestionParser
{
	
	public static List<QuestionGenerator> ParseCompleteComparisonQuestions(){
		return ParseQuestions("CompleteComparisonQuestions", QuestionGenerator.GeneratorType.Sorted, 13, 11, 12, -1);
	}
	
	public static List<QuestionGenerator> ParseMatchingQuestions(){
		return ParseQuestions("MatchingQuestions", QuestionGenerator.GeneratorType.Match, 9, -1, -1, 7);
	}
	
	public static List<QuestionGenerator> ParseTopNComparisonQuestions(){
		return ParseQuestions("TopNComparisonQuestions", QuestionGenerator.GeneratorType.Sorted, 13, 12, 11, -1);
	}
	
	public static List<QuestionGenerator> ParseRegionalTopNComparisonQuestions(){
		return SetWeight(0.2f, ParseQuestions("RegionalTopNComparisonQuestions", QuestionGenerator.GeneratorType.Sorted, 12, -1, 11, -1));
	}
	
	public static List<QuestionGenerator> ParseMultisetComparisonQuestions(){
		return ParseQuestions("MultisetComparisonQuestions", QuestionGenerator.GeneratorType.SortedMultiset, 12, -1, 11, -1);
	}
	
	

	private static List<QuestionGenerator> ParseQuestions(string filename, QuestionGenerator.GeneratorType type, int answersLine, int prefixesLine, int suffixesLine, int adjacentLine){
		List<QuestionGenerator> generators = new List<QuestionGenerator>();
		
		string filePath = Path.Combine(Path.Combine(Application.dataPath, "StreamingAssets"), filename + ".txt");
		StreamReader fileStream = new StreamReader(new FileStream(filePath, FileMode.Open));
		string file = fileStream.ReadToEnd();
		string separator = file.Contains("\r") ? "\r\n" : "\n";
		string[] fileLines = file.Split(new string[] { separator }, System.StringSplitOptions.None);
		
		// Line 0 contains all the questions.
		string[] cells = fileLines[0].Split(new char[] { '\t' }, System.StringSplitOptions.None);
		int questionCount = (cells.Length - 1) / 2;
		for(int n = 0; n < questionCount; n++){
			generators.Add(new QuestionGenerator());
			generators[n].m_questionText = cells[2 * n + 1];
		}
		
		// Line containing infoPrefixes.
		if(prefixesLine >= 0){
			cells = fileLines[prefixesLine].Split(new char[] { '\t' }, System.StringSplitOptions.None);
			for(int n = 0; n < questionCount; n++){
				generators[n].m_infoPrefix = cells[2 * n + 2];
			}
		}
		
		// Line containing infoSuffixes.
		if(suffixesLine >= 0){
			cells = fileLines[suffixesLine].Split(new char[] { '\t' }, System.StringSplitOptions.None);
			for(int n = 0; n < questionCount; n++){
				generators[n].m_infoSuffix = cells[2 * n + 2];
			}
		}
		
		// Line containing adjacentWithin.
		if(adjacentLine >= 0){
			cells = fileLines[adjacentLine].Split(new char[] { '\t' }, System.StringSplitOptions.None);
			for(int n = 0; n < questionCount; n++){
				if(cells[2 * n + 1].Length <= 0) continue;
				generators[n].m_adjacentWithin = int.Parse(cells[2 * n + 1]);
			}
		}
		
		// Lines containing the data.
		for(int line = answersLine; line < fileLines.Length; line++){
			cells = fileLines[line].Split(new char[] { '\t' }, System.StringSplitOptions.None);
			if(cells[0].Equals("truncate at")) break;
			for(int n = 0; n < questionCount; n++){
				generators[n].AddAnswer(cells[2 * n + 1], cells[2 * n + 2], type == QuestionGenerator.GeneratorType.SortedMultiset);
			}
		}
		
		for(int n = generators.Count - 1; n >= 0; n--){
			generators[n].m_type = type;
			if(generators[n].AnswersCount <= 0) generators.RemoveAt(n);
		}
		
		return generators;
	}
	
	private static List<QuestionGenerator> SetWeight(float weight, List<QuestionGenerator> generators){
		foreach(QuestionGenerator generator in generators){
			generator.m_weight = weight;
		}
		return generators;
	}
}
