using System;
using System.Collections.Generic;
using System.Xml.Linq;
using DarkRift;
using DarkRift.Server;
using Scripts.Models;
using Scripts.Networking;

namespace TicTacToeServerPlugin {
	public class TicTacToePlugin : Plugin {
		public override bool ThreadSafe => false;
		public override Version Version => new Version(0,0,1);

		private PlayerModel pendingPlayer;
		private Dictionary<int, MatchModel> matches;

		public TicTacToePlugin(PluginLoadData pluginLoadData) : base(pluginLoadData) {

			ClientManager.ClientConnected += OnClientConnected;
			ClientManager.ClientDisconnected += OnClientDisconnected;

			matches = new Dictionary<int, MatchModel>();
		}

		private void OnClientConnected(object sender, ClientConnectedEventArgs e) {
			WriteEvent("hello friend, " + e.Client.ID, DarkRift.LogType.Info);
			e.Client.MessageReceived += OnClientMessageReceived;
		}

		private void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e) {
			WriteEvent("goodbye friend, " + e.Client.ID, DarkRift.LogType.Info);

			if (pendingPlayer != null && pendingPlayer.Client == e.Client) {
				pendingPlayer = null;
			}

		}

		private void OnClientMessageReceived(object sender, MessageReceivedEventArgs e) {
			switch(e.Tag) {
				case (ushort)Tags.Tag.SET_NAME:

					// new player registering

					using (Message message = e.GetMessage()) {
						using (DarkRiftReader reader = message.GetReader()) {
							string name = reader.ReadString();
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine("hey mister " + name);
							Console.ForegroundColor = ConsoleColor.White;
							 
							PlayerModel newPlayer = new PlayerModel(e.Client, name);

							if (pendingPlayer == null) {
								// new player is pending for a match
								pendingPlayer = newPlayer;
							} else {
								// there is already a pending player. lets start a new match

								MatchModel match = new MatchModel(newPlayer);
								matches.Add(match.id, match);

								// report clients of the new match
								using (DarkRiftWriter writer = DarkRiftWriter.Create()) {
									writer.Write(match.id);
									writer.Write(match.CurrentPlayerClientID);
									using (Message msg = Message.Create((ushort)Tags.Tag.GOT_MATCH, writer)) {
										pendingPlayer.Client.SendMessage(msg, SendMode.Reliable);
										newPlayer.Client.SendMessage(msg, SendMode.Reliable);
									}
								}
								
								pendingPlayer = null;
							}
						}
					}
					break;

                case (ushort)Tags.Tag.CREATE_MATCH:

					// new player registering
                    Console.WriteLine("server - new match request received");

                    using (Message message = e.GetMessage())
                    {
                        using (DarkRiftReader reader = message.GetReader())
                        {
                            string name = reader.ReadString();
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("hey mister " + name);
                            Console.ForegroundColor = ConsoleColor.White;


							// 1. Create Player
							PlayerModel newPlayer = new PlayerModel(e.Client, name);

                            // 2. Create Match

                            MatchModel match = new MatchModel(newPlayer);
                            matches.Add(match.id, match);

                            Console.WriteLine("server new match ID" + match.id);


                            // report clients of the new match
                            using (DarkRiftWriter writer = DarkRiftWriter.Create())
                            {
                                writer.Write(match.id);
                                writer.Write(match.CurrentPlayerClientID);
                                using (Message msg = Message.Create((ushort)Tags.Tag.MATCHED_CREATED, writer))
                                {
                                    newPlayer.Client.SendMessage(msg, SendMode.Reliable);
                                }
                            }
                        }
                    }
                    break;

                case (ushort)Tags.Tag.JOIN_MATCH:

                    // new player registering
                    Console.WriteLine("server - join match request received");

                    using (Message message = e.GetMessage())
                    {
                        using (DarkRiftReader reader = message.GetReader())
                        {
                            string name = reader.ReadString();
                            ushort matchId = reader.ReadUInt16();

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("hey mister " + name + "you want to join match: " + matchId);
                            Console.ForegroundColor = ConsoleColor.White;

                            // 1. Create Player
                            PlayerModel newPlayer = new PlayerModel(e.Client, name);

                            // 2. Add player to existing match

                            if (matches.ContainsKey(matchId))
                            {
                                MatchModel match = matches[matchId];
                                match.AddPlayerToMatch(newPlayer);
								match.StartMatch();

                            }

                        }
                    }
                    break;

                case (ushort)Tags.Tag.SLATE_TAKEN:

					using (Message message = e.GetMessage()) {
						using (DarkRiftReader reader = message.GetReader()) {

							int matchId = reader.ReadUInt16();
							ushort slateIndex = reader.ReadUInt16();

							if (matches.ContainsKey(matchId)) {
								MatchModel match = matches[matchId];
								match.PlayerTakesSlate(slateIndex, e.Client);
								if (match.MatchOver) {
									// match is over
									Console.WriteLine($"match over. had: {matches.Count} matches");
									matches.Remove(matchId);
									Console.WriteLine($"match over. now have: {matches.Count} matches");
								}
							}
						}
					}

					break;
			}
		}
	}
}
