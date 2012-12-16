using UnityEngine;
using System.Collections;

public class Answer {
	
	public static double CLOSE_ENOUGH = 5.0;

	public double percentConfidence;//50-100
	public double actualPercent;//0,50,100
	public int score;
	
	public Answer(){
	}
	
	public Answer(double probGiven, bool correctAnswer, int score){
		this.percentConfidence = probGiven;
		this.actualPercent = correctAnswer ? 100.0 : 0.0;
		this.score = score;
	}
}
