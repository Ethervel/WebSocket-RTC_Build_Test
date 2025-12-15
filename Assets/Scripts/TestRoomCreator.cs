using UnityEngine;

public class TestRoomCreator : MonoBehaviour
{
    private string roomCodeInput = "";
    
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        
        // Style pour texte plus grand
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 16;
        
        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
        textFieldStyle.fontSize = 16;
        
        // Afficher l'état de connexion
        GUILayout.Label($"Connected: {VRNetworkManager.IsConnected}", labelStyle);
        GUILayout.Label($"In Room: {VRRoomManager.Instance?.IsInRoom}", labelStyle);
        GUILayout.Label($"Room ID: {VRRoomManager.Instance?.CurrentRoomId ?? "None"}", labelStyle);
        GUILayout.Label($"Players: {VRRoomManager.Instance?.PlayerCount ?? 0}", labelStyle);
        
        GUILayout.Space(20);
        
        // Si pas dans une room, afficher les options
        if (VRRoomManager.Instance != null && !VRRoomManager.Instance.IsInRoom)
        {
            // Bouton pour créer une room
            if (GUILayout.Button("Create Room", buttonStyle, GUILayout.Height(50)))
            {
                VRRoomManager.Instance.CreateRoom(RoomType.Lobby, "Test Room");
            }
            
            GUILayout.Space(20);
            
            // Champ pour entrer le code
            GUILayout.Label("Enter Room Code:", labelStyle);
            roomCodeInput = GUILayout.TextField(roomCodeInput, 6, textFieldStyle, GUILayout.Height(40));
            roomCodeInput = roomCodeInput.ToUpper();
            
            // Bouton pour rejoindre
            if (GUILayout.Button("Join Room", buttonStyle, GUILayout.Height(50)))
            {
                if (!string.IsNullOrEmpty(roomCodeInput))
                {
                    VRRoomManager.Instance.JoinRoom(roomCodeInput);
                }
            }
        }
        else
        {
            // Bouton pour quitter la room
            if (GUILayout.Button("Leave Room", buttonStyle, GUILayout.Height(50)))
            {
                VRRoomManager.Instance?.LeaveRoom();
            }
        }
        
        GUILayout.EndArea();
    }
}