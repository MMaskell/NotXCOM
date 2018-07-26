﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class GameController : MonoBehaviour {

    TileController[,,] heightmap;

    public int width = 20;
    public int height = 20;
    public int depth = 20;

    public GameObject tile;

    public Material baseMaterial;
    public Material darkMaterial;
    public Material startMaterial;
    public Material endMaterial;

    public Material checkingMaterial;

    private Vector3Int rayStart;
    private GameObject tileStart;

    private Vector3Int rayEnd;
    private GameObject tileEnd;

    private bool placingStart = true;

    public GameObject line;

    public bool clearLOS = true;

    public float maxTraversableCover = 0.5f;

    public bool HasLineOfSightDDA(Vector3Int start, Vector3Int end, out byte cover) {
        //Iterate from start position to end position, checking for blocking cover
        //TODO: Allow for stepping out
        //Full cover between blocks completely
        //Half cover between doesn't block but increases cover value
        //No cover between causes flanks

        //TODO: Fix 45 degree being dependant on direction
        line.GetComponent<LineRenderer>().SetPosition(0, start + new Vector3(0, 0.5f, 0));
        line.GetComponent<LineRenderer>().SetPosition(1, end + new Vector3(0, 0.5f, 0));
        //Get Ray direction
        float dirX = end.x - start.x;
        float dirY = end.y - start.y;
        float dirZ = end.z - start.z;

        int stepX = dirX >= 0 ? 1 : -1;
        int stepY = dirY >= 0 ? 1 : -1;
        int stepZ = dirZ >= 0 ? 1 : -1;

        float deltaX = stepX / dirX;
        float deltaY = stepY / dirY;
        float deltaZ = stepZ / dirZ;

        Vector3Int grid = start;

        float maxX = ((grid.x + Mathf.Max(0, stepX)) - (start.x + 0.5f)) / dirX;
        float maxY = ((grid.y + Mathf.Max(0, stepY)) - (start.y + 0.5f)) / dirY;
        float maxZ = ((grid.z + Mathf.Max(0, stepZ)) - (start.z + 0.5f)) / dirZ;

        bool stillSearching = true;

        cover = (byte)CoverType.NONE;

        Vector3Int oldGrid = new Vector3Int();
        while (stillSearching) {
            oldGrid = grid;
            //Which face of the tile was passed through
            //0 = NegX, 1 = PosX, 2 = NegY, 3 = PosY, 4 = NegZ, 5 = PosZ
            byte faceCurrent;
            byte faceNext;
            if (maxX < maxY) {
                if (maxX < maxZ) {
                    //Change in X
                    if (stepX < 0) {
                        faceCurrent = 0;
                        faceNext = 1;
                    } else {
                        faceCurrent = 1;
                        faceNext = 0;
                    }
                    grid.x = grid.x + stepX;
                    maxX = maxX + deltaX;
                } else {
                    //Change in Z
                    if (stepZ < 0) {
                        faceCurrent = 4;
                        faceNext = 5;
                    } else {
                        faceCurrent = 5;
                        faceNext = 4;
                    }
                    grid.z = grid.z + stepZ;
                    maxZ = maxZ + deltaZ;
                }
            } else {
                if (maxY < maxZ) {
                    //Change in Y
                    if (stepY < 0) {
                        faceCurrent = 2;
                        faceNext = 3;
                    } else {
                        faceCurrent = 3;
                        faceNext = 2;
                    }
                    grid.y = grid.y + stepY;
                    maxY = maxY + deltaY;
                } else {
                    //Change in Z
                    if (stepZ < 0) {
                        faceCurrent = 4;
                        faceNext = 5;
                    } else {
                        faceCurrent = 5;
                        faceNext = 4;
                    }
                    grid.z = grid.z + stepZ;
                    maxZ = maxZ + deltaZ;
                }
            }
            //Update current tile (DEBUG)
            UpdateEdges(oldGrid, faceCurrent);
            //Update next tile (DEBUG)
            UpdateEdges(grid, faceNext);
            //Break out if we hit the goal
            stillSearching = grid.x != end.x || grid.y != end.y || grid.z != end.z;
            if (stillSearching) {
                //Update cover value
                cover = Math.Max(cover, GetCoverValue(oldGrid, grid, faceCurrent, faceNext));
                //Check for cover
                if (cover == (byte)CoverType.FULL) {
                    return false;
                }
            }
        }
        return true;
    }

    byte GetCoverValue(Vector3Int t1, Vector3Int t2, byte f1, byte f2) {
        //Get the tile controllers for both tiles to check
        TileController tCon1 = GetTileController(t1), tCon2 = GetTileController(t2);
        if(tCon1 == null || tCon2 == null) {
            Debug.LogWarning("Missing tile controller!");
            return 0;
        }
        //Return the heighest cover value from the two tiles
        return Math.Max(tCon1.cover.GetCover(f1), tCon2.cover.GetCover(f2));
    }

    public Vector3Int[] FindPath(Vector3Int start, Vector3Int end) {
        //Track visited nodes
        List<Vector3Int> closed = new List<Vector3Int>();
        //Track potential nodes
        PriorityQueue<Vector3Int> open = new PriorityQueue<Vector3Int>();
        open.Enqueue(start, 0.0f);
        //Track best previous step
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        //Track g-scores for visiting each node
        Dictionary<Vector3Int, float> gScore = new Dictionary<Vector3Int, float>();
        //Set cost of first node to 0
        gScore[start] = 0.0f;
        while (open.Count > 0) {
            //Get node with lowest cost
            Vector3Int current = open.Dequeue();
            //If goal construct path
            if (current == end) {
                return ConstructPath(cameFrom, current);
            }
            //Remove current node and add to closed
            closed.Add(current);
            //For every neighbouring node
            for (int x = -1; x <= 1; x++) {
                for (int z = -1; z <= 1; z++) {
                    //A node is not a neighbour of itself
                    if (x == 0 && z == 0) {
                        continue;
                    }
                    //No diagonals
                    if (x != 0 && z != 0) {
                        continue;
                    }
                    //TODO: Change path finding to allow rising and falling
                    int y = 0;
                    Vector3Int neighbour = current + new Vector3Int(x, y, z);
                    //Skip out of bounds cells
                    if (neighbour.x < 0 || neighbour.y < 0 || neighbour.z < 0 || neighbour.x >= width || neighbour.y >= height || neighbour.z >= depth) {
                        continue;
                    }

                    //Check for walls blocking path
                    if (!CanTraverse(current, neighbour)) {
                        continue;
                    }

                    //If neighbour is closed, skip
                    if (closed.Contains(neighbour)) {
                        continue;
                    }
                    //Calculate distance from start to neighbour
                    float tentG = gScore[current] + 1.0f;
                    //Add neighbour to open if not in already
                    if (!open.Contains(neighbour)) {
                        open.Enqueue(neighbour, tentG + (neighbour - end).sqrMagnitude);
                    } else if (gScore.ContainsKey(neighbour) && tentG >= gScore[neighbour]) {
                        //Otherwise if score is greater than current, skip
                        continue;
                    }
                    //Update neighbour's camefrom to this node
                    cameFrom[neighbour] = current;
                    //Set neighbours g-score to new calculated
                    gScore[neighbour] = tentG;
                }
            }
        }
        return null;
    }

    //Extracts the shortest path from the distances calculated by A*
    Vector3Int[] ConstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current) {
        //Add current to path
        List<Vector3Int> path = new List<Vector3Int> {
            current
        };
        //While current exists in camefrom
        while (cameFrom.ContainsKey(current)) {
            //Set current to value in came from map
            current = cameFrom[current];
            //Add current to path
            path.Add(current);
        }
        return path.ToArray();
    }

    //Returns a list of every cell that can be reached in one step 
    List<Vector3Int> GetNeighbours(Vector3Int cell) {
        /*
         * Valid neighbour cells:
         * Have a floor tile (NegY)
         * Are 1 tile away (no diagonals) horizontally
         * Are at most MaxClimb tiles away up
         * Are at most MaxFall tiles away down
         * Have no full cover between the start cell and neighbour
         */
        List<Vector3Int> neighbours = new List<Vector3Int>();


        return neighbours;
    }

    //Tests if a unit can move from one tile to the other
    bool CanTraverse(Vector3Int first, Vector3Int last) {
        if (first.x != last.x && first.y != last.y && first.z != last.z) {
            //These tiles aren't neighbours!
            return false;
        }
        if (first.x == last.x && first.y == last.y && first.z == last.z) {
            //Why would you want to check if a tile can reach itself?
            return true;
        }
        TileController firstTile = GetTileController(first);
        TileController lastTile = GetTileController(last);
        //Moving Left
        if (last.x < first.x) {
            if (firstTile.cover.negativeX == (byte)CoverType.FULL) {
                return false;
            }
            if (lastTile.cover.positiveX == (byte)CoverType.FULL) {
                return false;
            }
        }
        //Moving Right
        if (last.x > first.x) {
            if (firstTile.cover.positiveX == (byte)CoverType.FULL) {
                return false;
            }
            if (lastTile.cover.negativeX == (byte)CoverType.FULL) {
                return false;
            }
        }
        //Moving Down
        if (last.y < first.y) {
            if (firstTile.cover.negativeZ == (byte)CoverType.FULL) {
                return false;
            }
            if (lastTile.cover.positiveZ == (byte)CoverType.FULL) {
                return false;
            }
        }
        //Moving Up
        if (last.y > first.y) {
            if (firstTile.cover.positiveZ == (byte)CoverType.FULL) {
                return false;
            }
            if (lastTile.cover.negativeZ == (byte)CoverType.FULL) {
                return false;
            }
        }
        return true;
    }

    void UpdateEdges(Vector3Int pos, int edge) {
        string edgeName;
        switch (edge) {
            case 0:
                edgeName = "NegX";
                break;
            case 1:
                edgeName = "PosX";
                break;
            case 2:
                edgeName = "NegY";
                break;
            case 3:
                edgeName = "PosY";
                break;
            case 4:
                edgeName = "NegZ";
                break;
            case 5:
                edgeName = "PosZ";
                break;
            default:
                edgeName = "ERR";
                break;
        }
        GameObject tile = GetTile(pos);
        if (!tile) {
            return;
        }
        Transform edgeTransform = tile.transform.Find(edgeName);
        if (!edgeTransform) {
            Debug.LogError("No edge with name " + edgeName);
        }
        edgeTransform.gameObject.GetComponent<Renderer>().material = checkingMaterial;
    }

    // Use this for initialization
    void Start() {
        heightmap = new TileController[width, height, depth];
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                for (int z = 0; z < depth; z++) {
                    //Associate grid positions
                    heightmap[x, y, z] = new TileController(this) {
                        gridPos = new Vector3Int(x, y, z)
                    };
                }
            }
        }
        GenerateTestBoard();
    }

    void SetTileObject(GameObject tile) {
        tile.transform.Find("NegX").gameObject.SetActive(false);
        tile.transform.Find("PosX").gameObject.SetActive(false);
        tile.transform.Find("NegY").gameObject.SetActive(false);
        tile.transform.Find("PosY").gameObject.SetActive(false);
        tile.transform.Find("NegZ").gameObject.SetActive(false);
        tile.transform.Find("PosZ").gameObject.SetActive(false);
    }

    //Creates a board with tiles on for testing
    void GenerateTestBoard() {
        //Ground floor
        for (int x = 0; x < width; x++) {
            for (int z = 0; z < depth; z++) {
                TileController tc = heightmap[x, 0, z];
                if (tc.Tile == null) {
                    GameObject newTile = Instantiate(tile, tc.gridPos, Quaternion.identity, gameObject.transform);
                    tc.Tile = newTile;
                    SetTileObject(newTile);
                }
                tc.cover.SetCover((byte)CoverSides.NEGY, (byte)CoverType.FULL);
                tc.Tile.transform.Find("NegY").gameObject.SetActive(true);
            }
        }
        //Small wall section
        for (int z = 10; z < width; z++) {
            int y = 0;
            int x = 10;
            TileController tc = heightmap[x, y, z];
            if (tc.Tile == null) {
                GameObject newTile = Instantiate(tile, tc.gridPos, Quaternion.identity, gameObject.transform);
                tc.Tile = newTile;
                SetTileObject(newTile);
            }
            tc.cover.SetCover((byte)CoverSides.NEGX, (byte)CoverType.FULL);
            tc.Tile.transform.Find("NegX").gameObject.SetActive(true);
        }
        //Small raised area
        for (int x = 10; x < width-5; x++) {
            for (int z = 0; z < depth-10; z++) {
                TileController tc = heightmap[x, 2, z];
                if (tc.Tile == null) {
                    GameObject newTile = Instantiate(tile, tc.gridPos, Quaternion.identity, gameObject.transform);
                    tc.Tile = newTile;
                    SetTileObject(newTile);
                }
                tc.cover.SetCover((byte)CoverSides.NEGY, (byte)CoverType.FULL);
                tc.Tile.transform.Find("NegY").gameObject.SetActive(true);
            }
        }
    }

    private void ClearLOS() {
        //If clearing has been disabled for some reason, stop
        if (!clearLOS) {
            return;
        }
        //Iterate over each tile and reset material properties
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                for (int z = 0; z < depth; z++) {
                    Vector3Int pos = new Vector3Int(x, y, z);
                    //Don't reset marker tiles (begininng and end of path)
                    if (pos == rayStart || pos == rayEnd) {
                        continue;
                    }
                    GameObject tileObj = GetTile(pos);
                    //Skip any tiles that don't have a corresponding game object
                    if (tileObj == null) {
                        continue;
                    }
                    ResetMaterials(tileObj);
                }
            }
        }
    }

    private void Update() {
        //Toggle begin/end node placement
        if (Input.GetKeyDown(KeyCode.Space)) {
            placingStart = !placingStart;
        }
        //Manually clear the board
        if (Input.GetKeyDown(KeyCode.LeftShift)) {
            bool prevClear = clearLOS;
            clearLOS = true;
            ClearLOS();
            clearLOS = prevClear;
        }
    }

    void SetMaterials(GameObject parent, Material mat) {
        //Loop through and mess with materials
        foreach (Renderer child in parent.GetComponentsInChildren<Renderer>()) {
            child.material = mat;
        }
    }

    void ResetMaterials(GameObject parent) {
        //Loop through and mess with materials
        foreach (Renderer child in parent.GetComponentsInChildren<Renderer>()) {
            //Floor/Ceiling tiles are lighter to add contrast
            if (child.gameObject.name.Contains("Y")) {
                child.material = baseMaterial;
            } else {
                child.material = darkMaterial;
            }
        }
    }

    public void TileClicked(Vector3Int tile) {
        //Wipe the board
        ClearLOS();
        //Place correct node
        if (placingStart) {
            if (tileStart) {
                ResetMaterials(tileStart);
            }
            rayStart = tile;
            tileStart = GetTile(tile);
            SetMaterials(tileStart, startMaterial);
        } else {
            if (tileEnd) {
                ResetMaterials(tileEnd);
            }
            rayEnd = tile;
            tileEnd = GetTile(tile);
            SetMaterials(tileEnd, endMaterial);
        }
        //byte cover;
        //bool los = HasLineOfSightDDA(rayStart, rayEnd, out cover);
        //Debug.Log(los ? "LOS " + cover : "No LOS");
        //*
        Vector3Int[] route = FindPath(rayStart, rayEnd);
        if (route != null) {
            foreach (Vector3Int pos in route) {
                if (pos != rayStart && pos != rayEnd) {
                    GameObject t = GetTile(pos);
                    if (t) {
                        SetMaterials(t, checkingMaterial);
                       // t.GetComponent<Renderer>().material = checkingMaterial;
                    }
                }
            }
        }
        //*/
    }

    //Gets the TileController for a specified tile (returns null if out of bounds)
    private TileController GetTileController(Vector3Int pos) {
        //Skip out of bounds
        if (pos.x < 0 || pos.y < 0 || pos.z < 0) {
            return null;
        }
        if (pos.x >= width || pos.y >= height || pos.z >= depth) {
            return null;
        }
        return heightmap[pos.x, pos.y, pos.z];
    }

    //Gets the Tile GameObject for a specified tile (if it exists)
    private GameObject GetTile(Vector3Int pos) {
        TileController tc = GetTileController(pos);
        if (tc == null) {
            return null;
        }
        return tc.Tile;
    }
}
