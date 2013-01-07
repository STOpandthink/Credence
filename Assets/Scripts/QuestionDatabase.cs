using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class QuestionDatabase {
	
	const string DATABASES_FILENAME = "Databases.txt";
	public static int globalFileIndex;
	public static List<QuestionDatabase> databases;

	public string name;
	public string url;
	public int fileIndex;
	public bool used;
	public bool downloaded;
	
	private static WWW currentRequest;
	private static float progress;
	public static bool loadingDatabases = false;
	public static bool justLoadedDatabases = false;
	public static string newDatabaseUrl = "";
	public static string resultLog = "";
	
	//Properties.
	public string FilePath{
		get { return Path.Combine(Application.streamingAssetsPath, fileIndex + ".xml"); }
	}
	public static float LoadingProgress{
		get { return progress + (currentRequest != null ? currentRequest.progress / databases.Count : 0f); }
	}
	public static float AddingProgress{
		get { return currentRequest != null ? currentRequest.progress : 0f; }
	}
	
	public override bool Equals(object obj){
		QuestionDatabase other = obj as QuestionDatabase;
		if(other == null) return false;
		return url.Equals(other.url);
	}
	
	public override int GetHashCode (){
		return url.GetHashCode();
	}
	
	//===== STATIC =====
	
	public static void RemoveUnselectedDatabases(){
		int numDeleted = 0;
		foreach(QuestionDatabase database in databases){
			if(database.used) continue;
			if(numDeleted >= databases.Count - 1) break;
			File.Delete(database.FilePath);
		}
		databases.RemoveAll(db => !db.used);
	}
	
	public static void SaveDatabases(){
		if(Application.isWebPlayer) return;
		StreamWriter writer = new StreamWriter(Path.Combine(Application.streamingAssetsPath, DATABASES_FILENAME));
		writer.WriteLine(globalFileIndex);
		writer.WriteLine(databases.Count);
		for(int n = 0; n < databases.Count; n++){
			QuestionDatabase database = databases[n];
			writer.WriteLine(database.name);
			writer.WriteLine(database.url);
			writer.WriteLine(database.fileIndex);
			writer.WriteLine(database.used);
			writer.WriteLine(database.downloaded);
		}
		writer.Close();
	}
	
	public static List<QuestionDatabase> LoadDatabases(){
		StreamReader reader = new StreamReader(Path.Combine(Application.streamingAssetsPath, DATABASES_FILENAME));
		List<QuestionDatabase> databases = LoadDatabasesFromStream(reader, true);
		reader.Close();
		return databases;
	}
	
	private static List<QuestionDatabase> LoadDatabasesFromStream(TextReader reader, bool isLocal){
		List<QuestionDatabase> databases = new List<QuestionDatabase>();
		if(isLocal) globalFileIndex = int.Parse(reader.ReadLine());
		int databasesCount = int.Parse(reader.ReadLine());
		for(int n = 0; n < databasesCount; n++){
			QuestionDatabase database = new QuestionDatabase();
			database.name = reader.ReadLine();
			database.url = reader.ReadLine();
			if(isLocal){
				database.fileIndex = int.Parse(reader.ReadLine());
				database.used = bool.Parse(reader.ReadLine());
				database.downloaded = bool.Parse(reader.ReadLine());
			}
			databases.Add(database);
		}
		return databases;
	}
	
	public static IEnumerator LoadDatabasesFromUrls(){
		loadingDatabases = true;
		progress = 0f;
		
		currentRequest = new WWW("http://appliedrationality.org/uploads/games/credence/Databases.txt");
		yield return currentRequest;
		if(currentRequest.error != null){
			resultLog = currentRequest.error;
		} else {
			TextReader reader = new StringReader(currentRequest.text);
			List<QuestionDatabase> newDatabases = LoadDatabasesFromStream(reader, false);
			reader.Close();
			int databasesAdded = 0;
			foreach(QuestionDatabase database in newDatabases){
				if(databases.Contains(database)) continue;
				database.fileIndex = globalFileIndex;
				globalFileIndex++;
				databases.Add(database);
				databasesAdded++;
			}
			
			foreach(QuestionDatabase database in databases){
				currentRequest = new WWW(database.url);
				yield return currentRequest;
				if(currentRequest.error != null){
					resultLog = currentRequest.error;
				} else {
					database.downloaded = true;
					StreamWriter writer = new StreamWriter(database.FilePath);
					writer.Write(currentRequest.text);
					writer.Close();
				}
				progress += 1f / databases.Count;
			}
			
			resultLog = "Updated all databases successfully." + (databasesAdded > 0 ? (" Added " + databasesAdded + " new databases.") : "");
			justLoadedDatabases = true;
		}
		loadingDatabases = false;
	}
	
	public static IEnumerator TryToAddNewDatabase(){
		if(databases.Exists(db => db.url.Equals(newDatabaseUrl))){
			resultLog = "Current database already exists.";
			yield break;
		}
		
		loadingDatabases = true;
		progress = 0f;
		
		currentRequest = new WWW(QuestionDatabase.newDatabaseUrl);
		yield return currentRequest;
		if(currentRequest.error != null){
			resultLog = "ERROR: " + currentRequest.error;
		} else {
			QuestionDatabase newDatabase = new QuestionDatabase();
			newDatabase.name = Path.GetFileNameWithoutExtension(QuestionDatabase.newDatabaseUrl);
			newDatabase.url = QuestionDatabase.newDatabaseUrl;
			newDatabase.fileIndex = globalFileIndex;
			newDatabase.used = true;
			newDatabase.downloaded = true;
			
			StreamWriter writer = new StreamWriter(newDatabase.FilePath);
			writer.Write(currentRequest.text);
			writer.Close();
			
			QuestionsScript.LoadQuestions(newDatabase);
			if(newDatabase.downloaded){
				globalFileIndex++;
				databases.Add(newDatabase);
				resultLog = "Added new database successfully.";
				justLoadedDatabases = true;
			} else {
				resultLog = "The database file is not in a valid format.";
				File.Delete(newDatabase.FilePath);
			}
		}
		loadingDatabases = false;
	}
}
