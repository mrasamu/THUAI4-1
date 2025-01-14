﻿using System;
using System.Collections;
using System.Threading;
using THUnity2D;

namespace GameEngine
{
	internal class CollisionChecker
	{

		/// <summary>
		/// 检查obj下一步位于nextPos时是否会与listObj碰撞
		/// </summary>
		/// <param name="obj">主动碰撞物，默认obj.Rigid为true</param>
		/// <param name="listObj">被动碰撞物</param>
		/// <param name="nextPos">obj下一步想走的位置</param>
		/// <returns>如果会碰撞，返回true</returns>
		public bool WillCollide(GameObject obj, GameObject listObj, XYPosition nextPos)
		{
			if (!listObj.IsRigid || listObj.ID == obj.ID) return false; //不检查自己和非刚体

			if (IgnoreCollision(obj, listObj)) return false;            // 可以忽略碰撞

			int deltaX = Math.Abs(nextPos.x - listObj.Position.x), deltaY = Math.Abs(nextPos.y - listObj.Position.y);

			//默认obj是圆形的，因为能移动的物体目前只有圆形；且会移动的道具尚未被捡起，其形状没有意义，可默认为圆形

			switch (listObj.Shape)
			{
				case GameObject.ShapeType.Circle:       //圆与圆碰撞
					{
						return (long)deltaX * deltaX + (long)deltaY * deltaY < ((long)obj.Radius + listObj.Radius) * ((long)obj.Radius + listObj.Radius);
					}
				case GameObject.ShapeType.Sqare:        //圆与正方形碰撞
					{
						if (deltaX >= listObj.Radius + obj.Radius || deltaY >= listObj.Radius + obj.Radius) return false;
						if (deltaX < listObj.Radius || deltaY < listObj.Radius) return true;
						return (long)(deltaX - listObj.Radius) * (long)(deltaY - listObj.Radius) < (long)obj.Radius * (long)obj.Radius;
					}
			}
			return false;
		}


		/// <summary>
		/// 碰撞检测，如果这样行走是否会与之碰撞，返回与之碰撞的物体
		/// </summary>
		/// <param name="obj">移动的物体</param>
		/// <param name="moveVec">移动的位移向量</param>
		/// <returns>和它碰撞的物体</returns>
		public GameObject? CheckCollision(GameObject obj, Vector moveVec)
		{
			XYPosition nextPos = obj.Position + Vector.Vector2XY(moveVec);

			if (!obj.IsRigid)
			{
				if (gameMap.OutOfBound(obj)) return new OutOfBoundBlock(nextPos);
				return null;
			}

			//在某列表中检查碰撞
			Func<ArrayList, ReaderWriterLockSlim, GameObject> CheckCollisionInList =
				(ArrayList lst, ReaderWriterLockSlim listLock) =>
				{
					GameObject? collisionObj = null;
					listLock.EnterReadLock();
					try
					{
						foreach (GameObject listObj in lst)
						{
							if (WillCollide(obj, listObj, nextPos))
							{
								collisionObj = listObj;
								break;
							}
						}
					}
					finally { listLock.ExitReadLock(); }
					return collisionObj;
				};

			GameObject collisionObj = null;
			foreach (var list in lists)
			{
				if ((collisionObj = CheckCollisionInList(list.Item1, list.Item2)) != null)
				{
					return collisionObj;
				}
			}

			//如果越界，则与越界方块碰撞
			if (gameMap.OutOfBound(obj))
			{
				return new OutOfBoundBlock(nextPos);
			}

			return null;
		}

		/// <summary>
		/// 寻找最大可能移动距离
		/// </summary>
		/// <param name="obj">移动物体，默认obj.Rigid为true</param>
		/// <param name="nextPos">下一步要到达的位置</param>
		/// <param name="moveVec">移动的位移向量，默认与nextPos协调</param>
		/// <returns>最大可能的移动距离</returns>
		public uint FindMax(GameObject obj, XYPosition nextPos, Vector moveVec)
		{
			uint maxLen = uint.MaxValue;
			uint tmpMax;
			foreach (var listWithLock in lists)
			{
				var lst = listWithLock.Item1;
				var listLock = listWithLock.Item2;
				listLock.EnterReadLock();
				try
				{
					foreach (GameObject listObj in lst)
					{
						//如果再走一步发生碰撞
						if (WillCollide(obj, listObj, nextPos))
						{
							switch (listObj.Shape)  //默认obj为圆形
							{
								case GameObject.ShapeType.Circle:
									{
										//计算两者之间的距离
										int orgDeltaX = listObj.Position.x - obj.Position.x;
										int orgDeltaY = listObj.Position.y - obj.Position.y;
										double mod = Math.Sqrt((long)orgDeltaX * orgDeltaX + (long)orgDeltaY * orgDeltaY);

										if (mod == 0.0)     //如果两者重合
										{
											tmpMax = 0;
										}
										else
										{
											Vector2 relativePosUnitVector = new Vector2(orgDeltaX / mod, orgDeltaY / mod);  //相对位置的单位向量
											Vector2 moveUnitVector = new Vector2(Math.Cos(moveVec.angle), Math.Sin(moveVec.angle)); //运动方向的单位向量
											if (relativePosUnitVector * moveUnitVector <= 0) continue;      //如果它们的内积小于零，即反向，那么不会发生碰撞
										}

										double tmp = mod - obj.Radius - listObj.Radius;
										if (tmp <= 0)           //如果它们已经贴合了，那么不能再走了
										{
											tmpMax = 0;
										}
										else
										{
											//计算最多能走的距离
											tmp = tmp / Math.Cos(Math.Atan2(orgDeltaY, orgDeltaX) - moveVec.angle);
											if (tmp < 0 || tmp > uint.MaxValue || tmp == double.NaN)
											{
												tmpMax = uint.MaxValue;
											}
											else tmpMax = (uint)tmp;
										}
										break;
									}
								case GameObject.ShapeType.Sqare:
									{
										//如果当前已经贴合，那么不能再行走了
										if (WillCollide(obj, listObj, obj.Position)) tmpMax = 0;
										else
										{
											//二分查找最大可能移动距离
											int left = 0, right = (int)moveVec.length;
											while (left < right - 1)
											{
												int mid = (right - left) / 2 + left;
												if (WillCollide(obj, listObj, obj.Position + new XYPosition((int)(mid * Math.Cos(moveVec.angle)), (int)(mid * Math.Sin(moveVec.angle)))))
												{
													right = mid;
												}
												else left = mid;
											}
											tmpMax = (uint)left;
										}
										break;
									}
								default:
									tmpMax = int.MaxValue;
									break;
							}

							if (tmpMax < maxLen) maxLen = tmpMax;
						}
					}
				}
				finally { listLock.ExitReadLock(); }
			}
			return maxLen;
		}

		Map gameMap;
		private Tuple<ArrayList, ReaderWriterLockSlim>[] lists;
		private Func<GameObject, GameObject, bool> IgnoreCollision;

		public CollisionChecker(Map gameMap, Func<GameObject, GameObject, bool> IgnoreCollision)
		{
			this.gameMap = gameMap;
			lists = new Tuple<ArrayList, ReaderWriterLockSlim>[]
			{
				new Tuple<ArrayList, ReaderWriterLockSlim>(gameMap.ObjList, gameMap.ObjListLock),
				new Tuple<ArrayList, ReaderWriterLockSlim>(gameMap.PlayerList, gameMap.PlayerListLock)
			};
			this.IgnoreCollision = IgnoreCollision;
		}
	}
}
