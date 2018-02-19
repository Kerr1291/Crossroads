using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System;

using UnityEngine;

//can use to search the whole computer for a folder, or use FindFiles to search a directory for all matching files
public class FileFinder
{
    ///Can use this to find all the matching files in a folder, good for locating stuff like dlls
    public static List<string> FindFiles( string searchRoot, string searchPattern, bool recursive = false )
    {
        SearchOption searchType = (recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        List < string > files = new List<string>();

        //search each folder in the root drive
        foreach( string file in Directory.EnumerateFiles( searchRoot, "*", searchType ) )
        {
            bool result = Path.GetFileName(file).Contains(searchPattern);

            if( result )
            {
                files.Add( file );
            }
        }

        return files;
    }

    bool searchCanceled = false;
    public void CancelSearch()
    {
        searchCanceled = true;
        if( tokenSource != null )
            tokenSource.Cancel();
    }

    public string SearchResult { get; private set; }
    public bool Running { get; private set; }

    System.Threading.CancellationTokenSource tokenSource;
    System.Threading.CancellationToken token;
    System.Object lockObj = new System.Object();

    public Action<string> OnFindCompleteCallback;

    protected class SearchParams
    {
        public string searchRoot;
        public string searchPattern;
    }

    void SearchDir( object input )
    {
        SearchParams sparams = input as SearchParams;

        ThreadedSearchDirectory( sparams );
    }

    void ThreadedSearchDirectory( SearchParams sparams )
    {
        string searchPattern = sparams.searchPattern;
        string searchRoot = sparams.searchRoot;
        
        //search each folder in the root drive
        //foreach( string file in Directory.EnumerateFiles( searchRoot, searchPattern, SearchOption.AllDirectories ) )
        foreach( string file in Directory.EnumerateDirectories( searchRoot, searchPattern, SearchOption.AllDirectories ) )
            {
            if( file.Contains( "Windows" )
                || file.Contains( "Temp" )
                || file.Contains( "System" )
                )
                continue;

            bool result = file.Contains(searchPattern);

            if( result )
            {
                lock( lockObj )
                {
                    SearchResult = file;
                }
                tokenSource.Cancel();
            }
        }
    }    

    public IEnumerator ThreadedFind(string fileToFind)
    {
        //Debug.LogError( "STARTING FIND" );
        searchCanceled = false;
        SearchResult = null;
        Running = true;
        tokenSource = new System.Threading.CancellationTokenSource();
        token = tokenSource.Token;

        //get all the root directories on each drive
        string searchPattern = "*";
        List<string> searchPaths = new List<string>();
        foreach( string drive in Directory.GetLogicalDrives() )
        {
            foreach( string directory in Directory.EnumerateDirectories( drive, searchPattern, SearchOption.TopDirectoryOnly ) )
            {
                if( directory.Contains( "Windows" )
                    || directory.Contains( "Temp" )
                    || directory.Contains( "System" )
                    || directory.Contains( "." )
                    || directory.Contains( "Recovery" )
                    )
                    continue;
                searchPaths.Add( directory );
            }
        }
        
        //going to run a threaded search task for each
        List<Task> taskList = new List<Task>();

        //search each folder in the root drive
        foreach( string directory in searchPaths )
        {
            SearchParams sparams = new SearchParams
            {
                searchRoot = directory,
                searchPattern = fileToFind
            };

            //Debug.LogError( "Searching " + directory );
            Task searchDir = new Task(SearchDir, sparams);
            taskList.Add( searchDir );
        }

        //Debug.LogError( "STARTING TASKS" );
        Debug.Log( "starting tasks " );
        foreach( Task t in taskList )
        {
            t.Start();
        }

        //give the tasks a moment to start
        yield return new WaitForSeconds(1f);

        Debug.Log( "Search is running..." );
        bool allTasksDone = false;
        List<int> running = new List<int>();
        while( !tokenSource.IsCancellationRequested )
        {
            if( allTasksDone )
                break;

            running.Clear();

            allTasksDone = true;

            //Debug.Log( "Search is running..." );
            //int i = 0;
            foreach( Task t in taskList )
            {
                if( t.Status == TaskStatus.Running )
                {
                    //running.Add( i );
                    allTasksDone = false;
                }
                //++i;
            }

            //string taskmsg = "Task ";
            //foreach( var v in running )
            //    taskmsg += searchPaths[ v ] + " ";
            //taskmsg += " is still running";
            //Debug.LogError( taskmsg );

            yield return null;
        }
        Debug.Log( "search done" );

        Running = false;

        if( searchCanceled )
            yield break;

        //Debug.Log( "SearchResult " );
        //Debug.Log( SearchResult );
        //no threads active here...
        if( !string.IsNullOrEmpty(SearchResult) )
        {
            //Debug.Log( "directory found" );
            //can either use this callback or check if it's still running and then pull the data from SearchResult
            if( OnFindCompleteCallback != null )
            {
                OnFindCompleteCallback.Invoke( SearchResult );
            }
        }
    }
}