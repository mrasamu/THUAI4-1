﻿using System.Threading;

namespace GameEngine
{
	public partial class Map
	{
		// 爱因斯坦说，每个坐标系都有与之绑定的时钟，(x, y, z, ict) 构成四维时空坐标，在洛伦兹变换下满足矢量性（狗头）
		private GameTimer timer = new GameTimer();
		public GameTimer Timer => timer;
		public class GameTimer
		{
			private bool isGaming;
			public bool IsGaming => isGaming;
			object isGamingLock = new object();

			public bool StartGame(int timeInMilliseconds)
			{
				lock (isGamingLock)
				{
					if (isGaming) return false;
					isGaming = true;
				}
				Thread.Sleep(timeInMilliseconds);
				isGaming = false;
				return true;
			}
		}
	}
}
