﻿using UnityEngine;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.IO;
using System;
using TMPro;
using JSON_card;
using JSON_client;
using JSON_server;
using UnityEngine.UI;

//Выполняет работу с сокетом, загружает данные с сервера. Занимается обработкой всех ответов сервера и вызовом всех
//необходимых для этого методов, методы можно указать извне. Ответы с сервера обычно получают после запроса
public class SocketNetwork : MonoBehaviour
{
    [Header("connect")]

    public string URL = "166.88.134.211"; //Айпи адрес сервера с бекендом
    public string connection_number = "9954"; //Порт подключения

    [Header("Debug")] //Не ставьте на false, если отлаживаете что-то связанное с сокетом
    public bool debugEnteringReqests = true;
    public bool debugExitingRequests = true;

    [Header("other")] //GameObject и экраны, с которыми связан сокет
    public MenuScreen m_menuScreen;
    public CardController card;
    private ScreenDirector _scrDirector;
    public AdminPabel adminPabel;
    public Room m_room;
    public RoomRow m_roomRow;
    public GameObject RoomPrefab;

    //Сбор статистики в админскую панель. Соответствующие объекты интерфейса
    public GameObject text_to_upd;
    public GameObject btn_for_upd;
    public TMP_Text server_debug_text;

    //"Истинный" сокет
    private WebSocket websocket;

    public int _type;

    //События. Можно добавить к ним свой обработчик, только не забыть убрать, чтобы сервер автоматически дообрабатывал, как вам надо
    //Используя функции из вашего класса
    #region events
    public delegate void RoomChangeEvent(uint[] FreeRoomsID);
    public static event RoomChangeEvent roomChange;

    public delegate void LoginEvent(string token, string name, uint UserID);
    public static event LoginEvent loginSucsessed;

    public delegate void SignInEvent();
    public static event SignInEvent SignInSucsessed;

    public delegate void emailChange(string email);
    public static event emailChange Sucsessed_emailChange;

    public delegate void chips(int chips);
    public static event chips gotChips;

    public delegate void games(int games);
    public static event games gotGames;

    public delegate void Error(string error);
    public static event Error error;

    public delegate void Raiting(List<RatingScreen.RatingLine> raiting);
    public static event Raiting gotRaiting;

    public delegate void Players(uint[] PlayersID);
    public static event Players changePlayers;
    public static event Players joinPlayer;

    public delegate void CardEvent(Card card);
    public static event CardEvent GetCard;
    public static event CardEvent DestroyCard;

    public delegate void deckEvents();
    public static event deckEvents colodaIsEmpty;
    public static event deckEvents trumpIsDone;

    public delegate void RoomEvents(Card trumpCard);
    public static event RoomEvents ready;

    public delegate void placeCardEvent(uint UID, Card card);
    public static event placeCardEvent placeCard;

    public delegate void BeatCardEvent(uint UID, Card beat, Card beating);
    public static event BeatCardEvent beatCard;

    public delegate void FoldCardEvent();
    public static event FoldCardEvent FoldAllCards;

    public delegate void atherUsersCards(uint uid);
    public static event atherUsersCards userGotCard;
    public static event atherUsersCards userDestroyCard;

    public delegate void GrabEvent();
    public static event GrabEvent playerGrab;

    public delegate void cl_foldEvent(uint UserID);
    public static event cl_foldEvent cl_fold;

    public delegate void cl_passEvent(uint UserID);
    public static event cl_passEvent cl_pass;

    public delegate void cl_grabEvent();
    public static event cl_grabEvent cl_grab;
    public static event cl_grabEvent grab;

    public delegate void gotAvagar(uint UserID, Sprite avatar);
    public static event gotAvagar got_avatar;

    public delegate void PlayerWon(uint UserID);
    public static event PlayerWon player_Win;

    public delegate void chat(uint ID, string message);
    public static event chat got_message;
    #endregion

    //Сюда лучше не лезть, умные люди уже заранее всё прописали
    void Start()
    {
        // Server initialization
        Debug.Log("Enter SocketNetwork start");

        string url = "ws://" + URL + ":" + connection_number;
        Debug.Log(url);

        //if (isOnLocalHost) url = "ws://localhost:9954";

        Debug.Log("Creating web socket");
        Debug.Log(url);
        websocket = new WebSocket(url);
        Debug.Log("Web socket created");

        websocket.OnOpen += (sender, e) =>
        {
            Debug.Log("WebSocket connection opened, Url: " + websocket.Url.ToString());
        };
        websocket.OnMessage += (sender, e) =>
        {
            string message = e.Data;

            /*if (debugEnteringReqests)*/ Debug.Log("get: " + message);

            HandleMessageFromServer(message);
        };
        websocket.OnError += (sender, e) =>
        {
            Debug.LogError("WebSocket: " + websocket.Url.ToString() + ", error: " + e.Exception + " : " + e.Message + " : " + sender);
        };
        websocket.OnClose += (sender, e) =>
        {
            Debug.LogError("WebSocket connection closed, Url: " + websocket.Url.ToString());
            Debug.LogError(e.Reason + ", code: " + e.Code + ", was clean: " + e.WasClean);
        };

        Debug.Log("Connecting");
        websocket.Connect();
        Debug.Log("Connected");

        //Обработчик событий для кнопки, которая запрашивает отчёт для админской панели сервера
        btn_for_upd.GetComponent<Button>().onClick.AddListener(() => {
            if (Session.Token == null) { Debug.LogError("No token"); return; }
            SendMessageToServer("getstats", new json_stats_out { token = Session.Token });
        });
    }

    #region  Server comunication

    //Получение ответа ПОСЛЕ ЗАПРОСА на сервер. Он СРАЗУ обработает в зависимости от типа события и указанных выше обработчиков
    void HandleMessageFromServer(string message)
    {
        JSON_client.MessageData data = JsonConvert.DeserializeObject<JSON_client.MessageData>(message);

        Debug.Log(data.ToString());

        switch (data.eventType)
        {

            //////////////////////////
            // room message handler //
            //////////////////////////

            case "cl_enterInTheRoom":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var enterRoomData = JsonConvert.DeserializeObject<JSON_client.JoinRoom>(data.data);

                    Session.RoomID = enterRoomData.RoomID;

                    GameObject Room = Instantiate(RoomPrefab);

                    m_room = Room.GetComponent<Room>();
                    m_roomRow = Room.GetComponent<RoomRow>();
                    card = m_room._cardController;

                    m_roomRow.RoomOwner = enterRoomData.roomOwnerID;

                    m_roomRow.GameType = (ETypeGame)enterRoomData.type;

                    m_roomRow.maxCards_number = enterRoomData.cards;

                    m_roomRow.maxPlayers_number = enterRoomData.maxPlayers;

                    m_roomRow.Bet = enterRoomData.bet;

                    m_roomRow.RoomID = enterRoomData.RoomID;
                });
                break;

            case "cl_enterInTheRoomAsOwner":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var enterRoomOwnerData = JsonConvert.DeserializeObject<JSON_client.JoinRoom>(data.data);

                    Session.RoomID = enterRoomOwnerData.RoomID;

                    GameObject Room = Instantiate(RoomPrefab);

                    m_room = Room.GetComponent<Room>();
                    m_roomRow = Room.GetComponent<RoomRow>();

                    m_roomRow.RoomOwner = Session.UId;

                    m_roomRow.GameType = (ETypeGame)enterRoomOwnerData.type;

                    m_roomRow.maxCards_number = enterRoomOwnerData.cards;

                    m_roomRow.maxPlayers_number = enterRoomOwnerData.maxPlayers;

                    m_roomRow.Bet = enterRoomOwnerData.bet;

                    m_roomRow.RoomID = enterRoomOwnerData.RoomID;
                });
                break;

            case "cl_startGameAlong":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    m_room.startGameAlone();
                });
                break;

            case "cl_joinRoom":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var joinRoomData = JsonConvert.DeserializeObject<JSON_client.PlayerJoin>(data.data);
                    m_room.NewPlayerJoin(joinRoomData.playerID);
                });
                break;

            case "cl_leaveRoom":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var joinRoomData = JsonConvert.DeserializeObject<JSON_client.PlayerExit>(data.data);
                    m_room.DeletePlayer(joinRoomData.playerID);
                });
                break;

            case "sucsessedLogin":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var loginData = JsonConvert.DeserializeObject<JSON_client.ClientLogin>(data.data);
                    loginSucsessed?.Invoke(loginData.token, loginData.name, loginData.UserID);
                });
                break;

            case "sucsessedSignIn":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    SignInSucsessed?.Invoke();
                });
                break;

            case "Sucsessed_emailChange":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var newEmail = JsonConvert.DeserializeObject<JSON_client.Sucsessed_emailChange>(data.data);

                    Sucsessed_emailChange?.Invoke(newEmail.newEmail);
                });
                break;

            case "FreeRooms":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var freeRoomsID = JsonConvert.DeserializeObject<JSON_client.FreeRooms>(data.data);
                    roomChange?.Invoke(freeRoomsID.FreeRoomsID);
                });
                break;

            case "roomPlayersID":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var playersID = JsonConvert.DeserializeObject<JSON_client.PlayersInRoom>(data.data);
                    changePlayers?.Invoke(playersID.PlayersID);
                });
                break;

            case "Chips":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var chips = JsonConvert.DeserializeObject<JSON_client.ClientData>(data.data);
                    gotChips?.Invoke(chips.chips);
                });
                break;

            case "got_rating":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    List<RatingScreen.RatingLine> raitingLine = JsonConvert.DeserializeObject<List<RatingScreen.RatingLine>>(data.data);
                    gotRaiting?.Invoke(raitingLine);
                });
                break;

            case "gameStats":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var games = JsonConvert.DeserializeObject<JSON_client.PlayedUserGames>(data.data);
                    gotGames?.Invoke(games.games);
                });
                break;

            case "ready":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var ready_data = JsonConvert.DeserializeObject<JSON_client.ClientReady>(data.data);

                    ready?.Invoke(ready_data.trump);
                });
                break;

            case "chat_message":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<JSON_client.GotMessage>(data.data);

                    got_message?.Invoke(_data.UserID, _data.message);
                });
                break;

            case "cl_getImage":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<JSON_client.AvatarData>(data.data);

                    byte[] imageBytes = System.Convert.FromBase64String(_data.avatarImage);

                    // Create a new Texture2D and load the image bytes
                    Texture2D texture = new Texture2D(1, 1);
                    texture.LoadImage(imageBytes);

                    // Create a Sprite using the loaded Texture2D
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);

                    got_avatar?.Invoke(_data.UserID, sprite);
                });
                break;

            case "GetCard":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var Card = JsonConvert.DeserializeObject<Card>(data.data);
                    GetCard?.Invoke(Card);
                });
                break;

            case "atherUserGotCard":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var user = JsonConvert.DeserializeObject<JSON_client.Client>(data.data);
                    userGotCard?.Invoke(user.UserID);
                });
                break;

            case "cl_gotCard":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var user = JsonConvert.DeserializeObject<JSON_client.Client>(data.data);
                    userGotCard?.Invoke(user.UserID);
                });
                break;

            case "cl_destroyCard":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var user = JsonConvert.DeserializeObject<JSON_client.Client>(data.data);
                    userDestroyCard?.Invoke(user.UserID);
                });
                break;

            case "DestroyCard":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<_card>(data.data);
                    DestroyCard?.Invoke(_data.card);
                });
                break;

            case "cl_role":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var role = JsonConvert.DeserializeObject<JSON_client.Role[]>(data.data);

                    foreach(Role _role in role)
                    {
                        if (_role.UserID == Session.UId)
                        {
                            Session.role = (ERole)_role.role;

                            if(Session.role == ERole.firstThrower || Session.role == ERole.thrower)
                            {
                                m_roomRow.GameUI.showFoldButton();
                            }

                            m_roomRow.status = EStatus.Null;
                        }
                        else
                        {
                            for(int i = 1; i < m_roomRow.roomPlayers.Count; i++)
                            {
                                if(m_roomRow.roomPlayers[i].UserID == _role.UserID)
                                {
                                    m_roomRow.roomPlayers[i].role = (ERole)_role.role;
                                }
                            }
                        }
                    }
                });
                break;

            case "cl_ThrowedCard":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<_card>(data.data);

                    placeCard?.Invoke(_data.UId, _data.card);
                });
                break;

            case "cl_BeatCard":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<JSON_client.Battle>(data.data);

                    beatCard?.Invoke(_data.UserID, _data.attacedCard, _data.attacingCard);
                });
                break;

            case "grab":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    playerGrab?.Invoke();
                });
                break;

            case "cl_grab":
                MainThreadDispatcher.RunOnMainThread(() => { 
                    cl_grab?.Invoke(); 
                });
                break;

            case "cl_playerFold":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<JSON_client.Client>(data.data);

                    cl_fold?.Invoke(_data.UserID);
                });
                break;

            case "cl_pass":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<JSON_client.Client>(data.data);

                    cl_pass?.Invoke(_data.UserID);
                });
                break;

            case "cl_fold":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    FoldAllCards?.Invoke();
                });
                break;

            case "coloda_empty":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    colodaIsEmpty?.Invoke();
                });
                break;

            case "trump_done":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    trumpIsDone?.Invoke();
                });
                break;

            case "playerWon": //Нужно доделать

                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var _data = JsonConvert.DeserializeObject<JSON_client.playerWon>(data.data);

                    player_Win?.Invoke(_data.UserID);
                });
                break;

            case "getstats":
                MainThreadDispatcher.RunOnMainThread(() =>
                {
                    Debug.Log("getting stats");

                    var _data = JsonConvert.DeserializeObject <string>(data.data);

                    Debug.Log("setting stats");
                    string fef = _data/*.text*/;

                    text_to_upd.GetComponent<TextMeshProUGUI>().text = fef;

                    Debug.Log("stats success");
                });
                break;               

            case "error":
                error?.Invoke(data.data);
                break;

            default:
                Debug.Log("Unknown event type: " + data.eventType);
                break;
        }
    }

    //Sends request
    public void SendMessageToServer(string eventType, object data)
    {
        Debug.Log("Send message to server");

        JSON_client.MessageData messageData = new JSON_client.MessageData()
        {
            eventType = eventType,
            data = JsonConvert.SerializeObject(data)
        };

        string json = JsonConvert.SerializeObject(messageData);
        if (websocket != null && websocket.IsAlive)
        {
            if (debugExitingRequests) Debug.Log("send: " + json);
            websocket.Send(json);
        }
        else
        {
            Debug.Log("Error: problems with socket");
        }
    }
    #endregion

    //Методы для обработки событий, связанных с комнатами. Отправка сообщений на сервер
    #region room emit functions 

    public void EmitCreateRoom(string token, int isPrivate, string key, uint bet, uint cards, int maxPlayers, ETypeGame type)
    {
        switch (type)
        {
            case ETypeGame.usual:
                _type = 0;
                break;

            case ETypeGame.ThrowIn:
                _type = 1;
                break;

            case ETypeGame.Transferable:
                _type = 2;
                break;

            default:
                return;
        }

        var createRoomData = new JSON_server.ServerCreateRoom()
        {
            token = token,
            isPrivate = isPrivate,
            key = key,
            bet = bet,
            cards = cards,
            type = _type,
            maxPlayers = maxPlayers,
            roomOwner = Session.UId
        };

        SendMessageToServer("srv_createRoom", createRoomData);
    }

    public void EmitJoinRoom(uint RoomID)
    {
        var joinRoomData = new JSON_server.ServerJoinRoom()
        {
            Token = Session.Token,
            key = "",
            RoomID = (uint)RoomID
        };

        SendMessageToServer("srv_joinRoom", joinRoomData);
    }

    public void EmitExitRoom(uint rid)
    {
        var exitRoomData = new JSON_server.ServerExitRoom()
        {
            token = Session.Token,
            rid = rid
        };

        SendMessageToServer("srv_exit", exitRoomData);
    }

    public void GetAllRoomPlayersID()
    {
        var exitRoomData = new JSON_server.UserData()
        {
            RoomID = Session.RoomID
        };

        SendMessageToServer("get_RoomPlayers", exitRoomData);
    }

    #endregion

    //Методы для обработки событий, связанных с ходами игроков. Отправка сообщений на сервер
    #region playing emit functions 

    public void EmitThrow(Card throw_Card)
    {
        var data = new JSON_server.Throw()
        {
            RoomID = Session.RoomID,
            UserID = Session.UId,

            card = throw_Card
        };

        SendMessageToServer("srv_Throw", data);
    }

    public void EmitBeat(Card beat, Card beating)
    {
        var data = new JSON_server.Battle()
        {
            RoomID = Session.RoomID,
            UserID = Session.UId,

            attackingCard = beating,
            attackedCard = beat
        };

        SendMessageToServer("srv_battle", data);
    }

    public void EmitReady(uint RoomID)
    {
        var readyData = new JSON_server.ServerJoinRoom()
        {
            RoomID = RoomID
        };

        SendMessageToServer("srv_ready", readyData);
    }

    public void EmitFold()
    {
        var foldData = new JSON_server.UserData()
        {
            token = Session.Token,
            RoomID = Session.RoomID
        };

        SendMessageToServer("srv_fold", foldData);
    }
    public void EmitPass()
    {
        var tokenData = new JSON_server.UserData()
        {
            RoomID = Session.RoomID,
            token = Session.Token
        };

        SendMessageToServer("srv_pass", tokenData);
    }
    public void EmitGrab()
    {
        var tokenData = new JSON_server.UserData()
        {
            RoomID = Session.RoomID,

            token = Session.Token
        };

        SendMessageToServer("srv_grab", tokenData);
    }

    #endregion

    //Методы для обработки событий, связанных с регистрацией и авторизацией. Отправка сообщений на сервер
    #region registration emit functions
    public void Emit_login(string _name, string _password)
    {
        var userData = new JSON_server.ClientLogin()
        {
            name = _name,
            password = _password
        };

        SendMessageToServer("Emit_login", userData);
    }

    public void Emit_signIn(string _name, string _email, string _password)
    {
        var userData = new JSON_server.ClientSignIN()
        {
            name = _name,
            password = _password,
            email = _email
        };

        SendMessageToServer("Emit_signIn", userData);
    }

    public void Emit_changeEmail(string token, string oldEmail, string newEmail)
    {
        var userData = new JSON_server.Client_changeEmail()
        {
            token = token,
            old_email = oldEmail,
            new_email = newEmail
        };

        SendMessageToServer("Emit_changeEmail", userData);
    }
    #endregion

    //Прочие запросы, связанные с получением некоторых данных
    #region get-emit functions

    public void GetFreeRooms()
    {
        SendMessageToServer("getFreeRooms", new UserData());
    }

    public void GetChips(string token)
    {
        var userData = new JSON_server.UserData()
        {
            token = token
        };

        SendMessageToServer("getChips", userData);
    }

    public void getAvatar(uint ID)
    {
        var requestData = new JSON_server.UserData()
        {
            UserID = ID
        };

        SendMessageToServer("getAvatar", requestData);
    }

    public void setAvatar(string imagePath)
    {
        byte[] imageBytes = File.ReadAllBytes(imagePath);
        string base64Image = Convert.ToBase64String(imageBytes);

        var avatarData = new JSON_server.AvatarData()
        {
            UserID = Session.UId,
            avatarImage = base64Image
        };
        
        SendMessageToServer("setAvatar", avatarData);
        Debug.Log("avatar is setted: " + avatarData.ToString());
    }

    public void get_gameStat()
    {
        var token = new JSON_server.UserData()
        {
            token = Session.Token
        };

        SendMessageToServer("get_gamesStat", token);
    }

    public void getRaiting()
    {
        var token = new JSON_server.UserData()
        {
            token = Session.Token
        };

        SendMessageToServer("get_raiting", token);
    }

    public void Emit_sendMessage(string message)
    {
        var data = new JSON_server.SendMessage()
        {
            RoomID = Session.RoomID,
            token = Session.Token, 
            message = message
        };

        SendMessageToServer("send_message", data);
    }

    public void admin_getChips(string token, int chips)
    {
        var userData = new JSON_server.get_chips()
        {
            token = token,
            chips = chips
        };

        SendMessageToServer("admin_getChips", userData);
    }
    #endregion

    //Если приложение закрыли, отправляем, что мы вышли из комнаты
    private void OnApplicationQuit()
    {
        if(Session.RoomID != 0)
        {
            EmitExitRoom(Session.RoomID);
        }
    }
}
