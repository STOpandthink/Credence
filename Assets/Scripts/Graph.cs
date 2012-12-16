using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Graph {
	
	// Static variables.
	private static double m_minX = 45.0, m_maxX = 100.0;
	private static double m_minY = 0.0, m_maxY = 100.0;
	private static float m_offsetX = 0f, m_offsetY = 0f;
	private static float m_width = 1f, m_height = 1.0f;
	private static float m_yAxisX = .07f, m_xAxisY = .9f;
	private static double m_scaleX, m_scaleY;
	private const float m_axisTextOffset = 0.01f;
	private const float m_hashSize = 0.02f;
	
	public static float OffsetX { get { return m_offsetX; } set { m_offsetX = value; ComputeWindow(); } }
	public static float Height { get { return m_height; } set { m_height = value; ComputeWindow(); } }
	
	static Graph(){
		ComputeWindow();
	}
	
	private static void ComputeWindow(){
		m_scaleX = (m_width - m_yAxisX - m_offsetX) / (double)(m_maxX - m_minX + 5.0);
		m_scaleY = (-m_xAxisY - m_offsetY) / (double)(m_maxY - m_minY + 10.0);
		m_xAxisY = .9f * m_height;
	}
	
	// Public variables.
	public double m_left, m_right;
	public double m_bottom, m_top;
	public double m_answerMin, m_answerMax, m_answer;
	
	
	public static void DrawAnswers(List<Bar> bars, Material material){
		if(bars.Count == 0) return;
		
		// Draw bar chart.
		GL.PushMatrix();
        material.SetPass(0);
        GL.LoadOrtho();
		
		// Draw arrows.
		GL.Begin(GL.LINES);
        foreach(Bar bar in bars){
			double barY = System.Math.Min(100.0, bar.y + 0.5);
			Vector3 origin = ConvertToPixel(bar.x, barY);
			Vector3 end = ConvertToPixel(System.Math.Max(barY, m_minX), barY);
			if(System.Math.Abs(bar.x - barY) <= Answer.CLOSE_ENOUGH) continue;
			int arrowSign = System.Math.Sign(end.x - origin.x);
			GL.Color(arrowSign > 0 ? Color.cyan : Color.magenta);
	        GLVertex(origin);
			GL.Color(Color.yellow);
	        GLVertex(end);
			GLVertex(end + new Vector3(-arrowSign * 10f / Screen.width, 10f / (float)Screen.height, 0f));
			GLVertex(end);
			GLVertex(end + new Vector3(-arrowSign * 10f / Screen.width, -10f / (float)Screen.height, 0f));
			GLVertex(end);
		}
		GL.End();
		
		DrawAxis(bars);
		GL.PopMatrix();
	}
	
	//Draw the x-y axis for the graph, along with axis hashmarks.
	private static void DrawAxis(List<Bar> bars){
		GL.Begin(GL.LINES);
        	GL.Color(Color.black);
			GLVertex3(m_yAxisX, 0.0f, 0f);
			GLVertex3(m_yAxisX, m_xAxisY, 0f);
			GLVertex3(0.0f, m_xAxisY, 0f);
			GLVertex3(m_width, m_xAxisY, 0f);
		GL.End();
		
		//Render hash marks for the Y axis.
		double hashY = 10.0;
		double y = m_minY;
		GL.Begin(GL.LINES);
		for(;; y += hashY){
			double pixelY = ConvertToPixelY(y);
			GLVertex3(m_yAxisX, pixelY, 0f);
			GLVertex3(m_yAxisX + m_hashSize, pixelY, 0f);
			if(y > m_maxY) break;
		}
		GL.End();
		
		//Compute values for the X axis.
		double hashX = 10.0;
		double x = 50.0;
		GL.Begin(GL.LINES);
		for(;; x += hashX){
			double pixelX = ConvertToPixelX(x);
			GLVertex3(pixelX, m_xAxisY - m_hashSize, 0f);
			GLVertex3(pixelX, m_xAxisY, 0f);
			if(x > m_maxX) break;
		}
		GL.End();
		
		// Draw optimal line.
		GL.Begin(GL.LINES);
		GL.Color(Color.yellow);
		GLVertex(ConvertToPixel(50.0, 50.0));
		GLVertex(ConvertToPixel(100.0, 100.0));
		GL.End();
		
		// Draw x-axis bar lines.
		GL.Begin(GL.LINES);
		GL.Color(Color.blue);
		foreach(Bar bar in bars){
			double barX = ConvertToPixelX(bar.x);
			GLVertex3(barX, m_xAxisY, 0f);
			GLVertex3(barX, m_xAxisY + m_hashSize, 0f);
			
			double leftX = ConvertToPixelX(bar.minX - 3.0), rightX = ConvertToPixelX(bar.maxX + 3.0);
			GLVertex3(leftX, m_xAxisY + m_hashSize, 0f);
			GLVertex3(rightX, m_xAxisY + m_hashSize, 0f);
			
			GLVertex3(leftX, m_xAxisY + m_hashSize, 0f);
			GLVertex3(leftX, m_xAxisY + 2f * m_hashSize, 0f);
			
			GLVertex3(rightX, m_xAxisY + m_hashSize, 0f);
			GLVertex3(rightX, m_xAxisY + 2f * m_hashSize, 0f);
		}
		GL.End();
	}
	
	public static int DrawAxisLabels(List<Bar> bars){
		// Render labels for bars.
		List<Rect> barLabelsX = new List<Rect>();
		foreach(Bar bar in bars){
			Vector3 pixel = ConvertToPixel(bar.x, bar.y);
			string valueString = bar.y.ToString("G3") + "%";
			Rect labelBounds = GetTextBounds(valueString, pixel);
			labelBounds.yMin -= labelBounds.height;
			GUI.Label(labelBounds, valueString, "xAxisSkin");
			
			float pixelX = ConvertToPixelX(bar.x);
			valueString = bar.x.ToString("G3") + "%";
			labelBounds = GetTextBoundsX(valueString + "||", pixelX);//+"||" for some space
			GUI.Label(labelBounds, valueString, "xAxisSkin");
			barLabelsX.Add(labelBounds);
		}
		
		// Render hash labels for Y axis.
		double hashY = 10.0;
		double y = m_minY;
		for(;; y += hashY){
			float pixelY = ConvertToPixelY(y);
			string valueString = ((int)y).ToString() + "%";
			Rect labelBounds = GetTextBoundsY(valueString, pixelY);
			if(y > m_maxY) break;
			if(labelBounds.yMax >= m_xAxisY * Screen.height) continue;
			GUI.Label(labelBounds, valueString, "yAxisSkin");
		}
		GUI.Label(GetTextBoundsY("Correct", ConvertToPixelY(y - hashY * 0.4)), "Correct", "yAxisSkin");
		
		// Render hash labels for X axis.
		double hashX = 10.0;
		double x = 50.0;
		for(;; x += hashX){
			float pixelX = ConvertToPixelX(x);
			string valueString = ((int)x).ToString() + "%";
			Rect labelBounds = GetTextBoundsX(valueString + "||", pixelX);//+"||" for some space
			if(x > m_maxX) break;
			if(barLabelsX.Exists((tempRect) => tempRect.Contains(new Vector2(labelBounds.xMin, labelBounds.center.y)) || tempRect.Contains(new Vector2(labelBounds.xMax, labelBounds.center.y)))) continue;
			GUI.Label(labelBounds, valueString, "xAxisSkin");
		}
		Rect labelRect = GetTextBoundsX("|Credence|", ConvertToPixelX(50.0));
		labelRect.x -= labelRect.width * 0.72f;
		GUI.Label(labelRect, "Credence:", "xAxisSkin");
		
		// Draw bars as buttons
		GUI.skin = GameScript.singleton.barSkin;
		GUI.color = Color.white;
		int selectedBar = -1;
		for(int n = 0; n < bars.Count; n++){
			Bar bar = bars[n];
			Vector3 pixelTL = ConvertToPixel(bar.x - 3.0, bar.y);
			Vector3 pixelBR = ConvertToPixel(bar.x + 3.0, 0.0);
			pixelTL.x = Screen.width * (pixelTL.x + m_offsetX);
			pixelTL.y = Screen.height * (pixelTL.y + m_offsetY);
			pixelBR.x = Screen.width * (pixelBR.x + m_offsetX);
			pixelBR.y = Screen.height * (pixelBR.y + m_offsetY);
			Rect rect = new Rect(pixelTL.x, pixelTL.y, pixelBR.x - pixelTL.x, pixelBR.y - pixelTL.y);
			if(GUI.Button(rect, "")) selectedBar = n;
			
			if(Input.GetMouseButtonUp(0) && (Screen.height - Input.mousePosition.y) < rect.yMin &&
				rect.xMin <= Input.mousePosition.x && Input.mousePosition.x <= rect.xMax){
				selectedBar = n;
			}
		}
		
		GUI.color = Color.white;
		GUI.skin = GameScript.singleton.defaultSkin;
		return selectedBar;
	}
	
	private static Rect GetTextBoundsX(string text, float x){
		float width = GUI.skin.label.CalcSize(new GUIContent(text)).x;
		float height = Screen.height * (m_height - m_xAxisY - m_axisTextOffset);
		x = Screen.width * (x + m_offsetX) - 0.5f * width;
		return new Rect(x, Screen.height * (m_xAxisY + 2.5f * m_axisTextOffset + m_offsetY), width, height);
	}
	
	private static Rect GetTextBoundsY(string text, float y){
		float width = Screen.width * (m_yAxisX - m_axisTextOffset);
		float height = GUI.skin.label.CalcHeight(new GUIContent(text), width);
		y = Screen.height * (y + m_offsetY) - 0.5f * height;
		return new Rect(m_offsetX * Screen.width, y, width, height);
	}
	
	private static Rect GetTextBounds(string text, Vector3 pos){
		float width = GUI.skin.label.CalcSize(new GUIContent(text)).x;
		float height = GUI.skin.label.CalcHeight(new GUIContent(text), width);
		pos.x = Screen.width * (pos.x + m_offsetX) - 0.5f * width;
		pos.y = Screen.height * (pos.y + m_offsetY);
		return new Rect(pos.x, pos.y, width, height);
	}
	
	//return - Graph coordinate (value) converted to Canvas coordinate.
	private static Vector3 ConvertToPixel(double x, double y){
		return new Vector3((float)ConvertToPixelX(x), (float)ConvertToPixelY(y), 0f);
	}
	
	//return - Graph x coordinate converted to Canvas x coordinate.
	private static float ConvertToPixelX(double x){
		return (float)((x - m_minX) * m_scaleX + m_yAxisX);
	}
	
	//return - Graph y coordinate converted to Canvas y coordinate.
	private static float ConvertToPixelY(double y){
		return (float)((y - m_minY) * m_scaleY + m_xAxisY);
	}
	
	private static void GLVertex3(double x, double y, double z){
		GL.Vertex3((float)x + m_offsetX, (float)(1.0 - y) + m_offsetY, (float)z);
	}
	
	private static void GLVertex(Vector3 p){
		GLVertex3(p.x, p.y, p.z);
	}
}