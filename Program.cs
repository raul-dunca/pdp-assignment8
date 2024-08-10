using System;
using MPI;

public class Graph
{
    private int nr_vertices;
    private int nr_edges;
    HashSet<(int, int)> edges;

    public Graph(int v, int e)
    {
        nr_vertices = v;
        nr_edges = e;
        edges = new HashSet<(int, int)>();
    }
    public int NumberOfVertices
    {
        get { return nr_vertices; }
        set
        {
            if (value < 0)
            {
                Console.WriteLine("Number of vertices cannot be negative.");
            }
            else
            {
                nr_vertices = value;
            }
        }
    }


    public int NumberOfEdges => nr_edges;

    public HashSet<(int, int)> Edges => edges;

    public bool EdgeExists(int x, int y)
    {
        return edges.Contains((x, y)) || edges.Contains((y, x));
    }
    public void AddEdge(int x, int y)
    {
        if (x < 0 || x >= nr_vertices || y < 0 || y >= nr_vertices)
        {
            Console.WriteLine("Vertices out of range");
        }
        else
        {
            edges.Add((x, y));
        }
    }

    public List<int> GetNeighbors(int node)
    {
        List<int> neighbors = new List<int>();

        foreach (var edge in edges)
        {
            if (edge.Item1 == node && !neighbors.Contains(edge.Item2))
            {
                neighbors.Add(edge.Item2);
            }
            else if (edge.Item2 == node && !neighbors.Contains(edge.Item1))
            {
                neighbors.Add(edge.Item1);
            }
        }

        return neighbors;
    }


    public void GenerateRandomGraph()
    {
        if (nr_edges > (nr_vertices * (nr_vertices - 1))/2)
        {
            Console.WriteLine("Number of edges exceeds the maximum possible for this graph");

        }
        else
        {
            Random rand = new Random();

            while (edges.Count < nr_edges)
            {
                int x = rand.Next(nr_vertices);
                int y = rand.Next(nr_vertices);

                if (!EdgeExists(x, y) && x != y)
                {
                    AddEdge(x, y);
                }
            }
        }
    }

    public void DisplayGraph()
    {
        Console.WriteLine("Directed Edges in the Graph:");
        foreach (var edge in edges)
        {
            Console.WriteLine($"{edge.Item1} {edge.Item2}");
        }
    }
}


class Program
{

    static Graph ReadGraphFromFile(string filePath)
    {
        using (StreamReader reader = new StreamReader(filePath))
        {
            string[] header = reader.ReadLine().Split();
            int nr_vertices = int.Parse(header[0]);
            int nr_edges = int.Parse(header[1]);

            Graph graph = new Graph(nr_vertices, nr_edges);

            for (int i = 0; i < nr_edges; i++)
            {
                string[] edge = reader.ReadLine().Split();
                int x = int.Parse(edge[0]);
                int y = int.Parse(edge[1]);
                graph.AddEdge(x, y);
            }

            return graph;
        }
    }

    static void coloring (int start_node, int end_node,List<int> colors, Graph graph)
    {
        List<bool> avaliable = Enumerable.Repeat(true, graph.NumberOfVertices).ToList();

        //int maxi = 0;
        //int start_here = 0;
        //for (int i= 0; i < graph.NumberOfVertices; i++)
        //{
        //    if (graph.GetNeighbors(i).Count > maxi)
        //    {
        //        start_here = i;
        ///        maxi = graph.GetNeighbors(i).Count;
        //    }
        //}

        colors[start_node] = 0;

        for (int node = start_node; node < end_node; node++)
        {
            List<int> neighbours = graph.GetNeighbors(node);
            foreach (int neighbor in neighbours)
            {
                if (neighbor >= start_node && neighbor < end_node && colors[neighbor] != -1)
                {
                    avaliable[colors[neighbor]] = false;
                }
            }

            int cr;
            for (cr = 0; cr < graph.NumberOfVertices; cr++)
            {
                if (avaliable[cr])
                    break;
            }

            colors[node] = cr;

            for (int i = 0; i < graph.NumberOfVertices; i++)
            {
                avaliable[i] = true;
            }
        }
    }

    static void coloringWList(List<int> nodes, List<int> colors, Graph graph)
    {
        List<bool> avaliable = Enumerable.Repeat(true, graph.NumberOfVertices).ToList();

        colors[nodes[0]] = 0;


        foreach (int node in nodes)
        {
            List<int> neighbours = graph.GetNeighbors(node);
            foreach (int neighbor in neighbours)
            {
                if (colors[neighbor] != -1)
                {
                    avaliable[colors[neighbor]] = false;
                }
            }

            int cr;
            for (cr = 0; cr < graph.NumberOfVertices; cr++)
            {
                if (avaliable[cr])
                    break;
            }

            colors[node] = cr;

            for (int i = 0; i < graph.NumberOfVertices; i++)
            {
                avaliable[i] = true;
            }
        }
    }
    static void Main(string[] args)
    {
        MPI.Environment mpiEnvironment = new MPI.Environment(ref args);

        int rank = Communicator.world.Rank;
        int size = Communicator.world.Size;

        string fileName = "graph.txt";
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
         Graph graph = ReadGraphFromFile(filePath);
        //Graph graph = new Graph(100,150);
        //graph.GenerateRandomGraph(); 
        int nodesPerProcess = graph.NumberOfVertices / size;
        int remainder = graph.NumberOfVertices % size;
        int n = 3;
        if (remainder != 0)
        {
            graph.NumberOfVertices = (nodesPerProcess + 1) * size;
            nodesPerProcess = graph.NumberOfVertices / size;
        }

        if (rank==0)
        {
            graph.DisplayGraph();
            int startNode_master = 0;
            int endNode_master = startNode_master + nodesPerProcess;
            int startNode = endNode_master;
            for (int i = 1; i < size; i++)
            {
                int endNode = startNode + nodesPerProcess;

                Communicator.world.Send(startNode, i, 0);
                Communicator.world.Send(endNode, i, 1);
                startNode = endNode;
            }

            Console.WriteLine($"Rank {rank} received startd node: {startNode_master} and end node {endNode_master}");

            List<int> colors = Enumerable.Repeat(-1, graph.NumberOfVertices).ToList();
            coloring(startNode_master, endNode_master, colors, graph);

            for (int i = endNode_master; i < graph.NumberOfVertices; i++)
            {
                int node = Communicator.world.Receive<int>(Communicator.anySource, 0);
                int color = Communicator.world.Receive<int>(Communicator.anySource, 1);
                colors[node] = color;
            }

            List<int> badColors = new List<int>();
            
            foreach ((int x, int y) in graph.Edges)
            {
                if (colors[x]==colors[y])
                {
                    if (!badColors.Contains(x))
                    {
                        badColors.Add(x);
                    }
                    if (!badColors.Contains(y))
                    {
                        badColors.Add(y);
                    }
                }
            }

            Console.WriteLine($"Conflicted nodes: {badColors.Count}");

            if (badColors.Count > 0)
            {
                coloringWList(badColors, colors, graph);
            }
            
            for (int i = 0; i < colors.Count; i++)
            {

                Console.WriteLine($"Idx: {i} and Color: {colors[i]}");
            }


            foreach ((int x, int y) in graph.Edges)
            {
                if (colors[x] == colors[y])
                {
                    Console.WriteLine("Not good!!!!");
                    break;
                }
            }
            Console.WriteLine("Good solution!");

            int chromatic_number = colors.Max();
            chromatic_number += 1;
            if (chromatic_number==n)
            {
                Console.WriteLine($"Its a {n}-coloring graph!");
            }

            else if (chromatic_number < n)
            {
                Console.WriteLine($"Its a {n}-colorable graph!");
            }
            else
            {
                Console.WriteLine($"Its not possible to {n}-color this graph!");
            }
        }
        else
        {
            int start_node = Communicator.world.Receive<int>(0, 0);
            int end_node = Communicator.world.Receive<int>(0, 1);

            Console.WriteLine($"Rank {rank} received startd node: {start_node} and end node {end_node}");

            List<int> colors = Enumerable.Repeat(-1, graph.NumberOfVertices).ToList();
            coloring(start_node, end_node, colors, graph);

            for (int i = start_node; i < end_node; i++)
            {
                Communicator.world.Send(i, 0, 0);
                Communicator.world.Send(colors[i], 0, 1);
            }
        }
        mpiEnvironment.Dispose();
    }
}
