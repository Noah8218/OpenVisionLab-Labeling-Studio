using OpenVisionLab.ImageCanvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.Overlays;
using OpenVisionLab.ImageCanvas.Rendering;
using SharpGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenVisionLab.ImageCanvas.OpenGLRendering
{
	public static class OpenGlOverlayExtensions
	{
		private const int ImmediateGroupBoundsRefreshLimit = 10_000;

		#region Overlay
		public static void AddOverlay(this ImageCanvasControl canvasViewer, string parentType, string childType, CanvasShape shape, string uniqueId, EnumInspWindowType InspType = EnumInspWindowType.Unit, EnumItemType itemType = EnumItemType.Window, bool isExtentionRectange = false, bool isGroupRectangle = false)
		{
			shape.UniqueId = uniqueId;
			shape.GroupType = childType;

			// ?덈줈??Shape瑜?異붽??⑸땲??
			CanvasOverlayItem overlayItem = CreateNewCanvasOverlayItem(canvasViewer, childType, shape, InspType, itemType, isExtentionRectange, isGroupRectangle);
			canvasViewer.GetCanvasOverlayManager().AddOverlayItem(parentType, overlayItem);

			if (ShouldRefreshGroupBoundsImmediately(canvasViewer, parentType))
			{
				ResizeGroupRectangle(canvasViewer, parentType);
				canvasViewer.InvalidateVisibleOverlayCache();
			}
			else
			{
				canvasViewer.AddVisibleOverlayIfInViewport(overlayItem);
			}
		}

		private static bool ShouldRefreshGroupBounds(ImageCanvasControl canvasViewer, string groupType)
		{
			if (string.IsNullOrWhiteSpace(groupType))
			{
				return false;
			}

			CanvasOverlayItem group = canvasViewer.GetCanvasOverlayManager().GetGroupToType(groupType);
			return group?.IsVisible == true;
		}

		private static bool ShouldRefreshGroupBoundsImmediately(ImageCanvasControl canvasViewer, string groupType)
		{
			if (!ShouldRefreshGroupBounds(canvasViewer, groupType))
			{
				return false;
			}

			CanvasOverlayItem group = canvasViewer.GetCanvasOverlayManager().GetGroupToType(groupType);
			return group == null || group.ChildObjects.Count <= ImmediateGroupBoundsRefreshLimit;
		}

		private static CanvasOverlayItem CreateNewCanvasOverlayItem(ImageCanvasControl canvasViewer, string childType, CanvasShape shape, EnumInspWindowType InspType, EnumItemType itemType, bool isExtentionRectange, bool isGroupRectangle)
		{
			System.Drawing.Color drawColor = System.Drawing.Color.White;
			// Group蹂??됱쓣 ?ㅼ젙?⑸땲??
			if (canvasViewer.GetCanvasOverlayManager().GroupBrushes.TryGetValue(InspType, out System.Windows.Media.SolidColorBrush brush))
			{
				drawColor = System.Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
			}

			CanvasOverlayItem newObject = new CanvasOverlayItem { GroupType = childType, Shape = shape, ItemType = itemType, InspWindowType = InspType, IsExtentionRectange = isExtentionRectange, IsGroupRectangle = isGroupRectangle, Color = drawColor };
			ConnectOverlayCallback(newObject, canvasViewer.GetOpenGL());

			return newObject;
		}

		public static void UpdateOverlays(this ImageCanvasControl canvasViewer)
		{
			// 紐⑤뱺 CanvasShape 媛앹껜瑜??쒗쉶?⑸땲??
			foreach (CanvasOverlayItem overlayItem in canvasViewer.GetCanvasOverlayManager().GetAllOverlays())
			{
				overlayItem.Shape.IsChanged = true;
			}
		}

		/// <summary>
		/// 蹂寃쎌젏??媛먯?媛 ?섎㈃ ?먮룞?쇰줈 ?낅뜲?댄듃?섎뒗 肄쒕깹???곌껐?⑸땲??
		/// </summary>
		/// <param name="newObject"></param>
		private static void ConnectOverlayCallback(CanvasOverlayItem newObject, OpenGL gl)
		{
			newObject.Shape.OnChanged = () =>
			{
				if (newObject.Shape.IsChanged)
				{
					OpenGlDrawing.CompileOverlayShape(gl, newObject);
					newObject.Shape.IsChanged = false;
				}
			};
			newObject.Shape.IsChanged = true;
			newObject.Shape.OnChanged?.Invoke();
		}

		public static void DeleteOverlay(this ImageCanvasControl canvasViewer, string uniqueId = "", string groupName = "", bool refreshImmediately = true)
		{
			CanvasOverlayItem overlayItem = canvasViewer.GetCanvasOverlayManager().GetOverlayByUniqueId(uniqueId);
			ReleaseDisplayList(canvasViewer.GetOpenGL(), overlayItem?.Shape);
			bool removed = canvasViewer.GetCanvasOverlayManager().RemoveOverlayByUniqueId(uniqueId);
			if (removed)
			{
				if (ShouldRefreshGroupBoundsImmediately(canvasViewer, groupName))
				{
					ResizeGroupRectangle(canvasViewer, groupName);
					canvasViewer.InvalidateVisibleOverlayCache();
				}
				else
				{
					// Single ROI delete must stay object-local. Large visible groups refresh their
					// decorative bounds on the next explicit group refresh instead of scanning all children here.
					canvasViewer.RemoveVisibleOverlay(overlayItem);
				}
			}
			if (refreshImmediately)
			{
				canvasViewer.RefreshGL();
			}
			else
			{
				canvasViewer.QueueRefreshGLAfterInput();
			}
		}

		private static void ReleaseDisplayList(OpenGL gl, CanvasShape shape)
		{
			if (gl == null || shape == null) { return; }

			if (shape.DisplayListId != 0)
			{
				gl.DeleteLists(shape.DisplayListId, 1);
				shape.DisplayListId = 0;
			}

			if (shape is CanvasRect<float> rect && rect.ExtendedRectangle != null && rect.ExtendedRectangle.DisplayListId != 0)
			{
				gl.DeleteLists(rect.ExtendedRectangle.DisplayListId, 1);
				rect.ExtendedRectangle.DisplayListId = 0;
			}
		}

		public static List<CanvasOverlayItem> GetVisibleUnlockedOverlays(this ImageCanvasControl canvasViewer)
		{
			return canvasViewer.GetCanvasOverlayManager().GetAllVisibleUnlockedOverlays().ToList();
		}

		private static bool CheckMoveWithinBounds(CanvasRect<float> roi, CanvasSize<float> moveSize, Size imageSize)
		{
			CanvasRect<float> check = (CanvasRect<float>)roi.Clone();
			check.OffsetMove(moveSize, false);
			return IsRoiWithinImageBounds(check, imageSize);
		}

		private static CanvasSize<float> ClampMoveWithinBounds(CanvasRect<float> roi, CanvasSize<float> moveSize, Size imageSize)
		{
			float width = moveSize.Width;
			float height = moveSize.Height;

			if (roi.Left + width < 0)
			{
				width = -roi.Left;
			}
			else if (roi.Right + width > imageSize.Width)
			{
				width = imageSize.Width - roi.Right;
			}

			if (roi.Bottom + height < 0)
			{
				height = -roi.Bottom;
			}
			else if (roi.Top + height > imageSize.Height)
			{
				height = imageSize.Height - roi.Top;
			}

			return new CanvasSize<float>(width, height);
		}

		/// <summary>
		/// ?곹븯醫뚯슦 諛⑺뼢??寃?ы븯??roi媛 ?대룞 媛?ν븳吏 寃?ы빀?덈떎.
		/// </summary>
		/// <param name="roi"></param>
		/// <param name="size"></param>
		/// <param name="imageSize"></param>
		/// <returns></returns>
		private static CanvasSize<float> GetPossibleMoveDirection(CanvasRect<float> roi, CanvasSize<float> size, Size imageSize)
		{
			bool canMoveLeft = CheckMoveWithinBounds(roi, new CanvasSize<float>(-Math.Abs(size.Width), 0), imageSize);
			bool canMoveRight = CheckMoveWithinBounds(roi, new CanvasSize<float>(Math.Abs(size.Width), 0), imageSize);
			bool canMoveUp = CheckMoveWithinBounds(roi, new CanvasSize<float>(0, Math.Abs(size.Height)), imageSize);
			bool canMoveDown = CheckMoveWithinBounds(roi, new CanvasSize<float>(0, -Math.Abs(size.Height)), imageSize);

			CanvasSize<float> move = new CanvasSize<float>(0, 0);
			if (canMoveLeft && size.Width < 0)
			{
				move.Width = size.Width;
			}
			else if (canMoveRight && size.Width > 0)
			{
				move.Width = size.Width;
			}

			if (canMoveUp && size.Height > 0)
			{
				move.Height = size.Height;
			}
			else if (canMoveDown && size.Height < 0)
			{
				move.Height = size.Height;
			}

			return move;
		}

		/// <summary>
		/// ROI媛 ?대?吏 ?ъ씠利덈? ?섏뼱??Move媛 ?덈릺?꾨줉 留됱뒿?덈떎.
		/// </summary>
		/// <param name="roi"></param>
		/// <param name="size"></param>
		/// <param name="imageSize"></param>
		public static void PerformRoiMove(this ImageCanvasControl canvasViewer, CanvasRect<float> roi, CanvasSize<float> size, Size imageSize, bool canMoveRoi, bool notify = true)
		{
			if (IsRoiWithinImageBounds(roi, imageSize))
			{
				CanvasSize<float> boundedMove = ClampMoveWithinBounds(roi, size, imageSize);
				if (boundedMove.Width != 0 || boundedMove.Height != 0)
				{
					roi.OffsetMove(boundedMove, notify); // roi move
				}
			}
			else
			{
				CanvasSize<float> move = GetPossibleMoveDirection(roi, size, imageSize);
				if (move.Width != 0 || move.Height != 0 || !canMoveRoi)
				{
					roi.OffsetMove(move, notify);
				}
				else
				{
					// ?대룞?????녿뒗 寃쎌슦 泥섎━
					roi.OffsetMove(size, notify);
				}
			}
		}

		private static bool IsRoiWithinImageBounds(CanvasRect<float> roi, Size imageSize)
		{
			// ?대? ?뺣젹??ROI ?먮뱾???ъ슜
			CanvasPoint<float> leftTop = roi.LeftTop;
			CanvasPoint<float> rightTop = roi.RightTop;
			CanvasPoint<float> rightBottom = roi.RightBottom;
			CanvasPoint<float> leftBottom = roi.LeftBottom;

			bool isRoiWithinImageBoundsX = false;
			bool isRoiWithinImageBoundsY = false;

			if (leftBottom.X >= 0 && rightTop.X <= imageSize.Width)// X異?泥댄겕
			{
				isRoiWithinImageBoundsX = true;
			}
			if ((leftTop.Y) <= imageSize.Height && (leftBottom.Y) >= 0)// Y異?泥댄겕
			{
				isRoiWithinImageBoundsY = true;
			}

			return isRoiWithinImageBoundsX & isRoiWithinImageBoundsY;
		}

		public static void ResizeGroupRectangle(this ImageCanvasControl canvasViewer, string targetGroup)
		{
			if (targetGroup == "") { return; }
			CanvasOverlayItem currentGroup = canvasViewer.GetCanvasOverlayManager().GetGroupToType(targetGroup);
			if (currentGroup == null) { return; }
			RectangleF rt = currentGroup.GetGroupRectangle(canvasViewer.GetCanvasOverlayManager().IsTopLevelObject(currentGroup));
			CanvasShape target = currentGroup.Shape;

			if (target != null)
			{
				// 怨꾩궛??Group Rectangle???ъ슜?⑸땲??
				(target as CanvasRect<float>).UpdateRectangle(0, 0, 0, 0);
				(target as CanvasRect<float>).UpdateRectangle(rt.Left, rt.Top, rt.Right, rt.Bottom);
				target.IsChanged = true;
				canvasViewer.UpdateInteractiveOverlayIndex(target);
			}
		}

		public static void SetLockControl(this ImageCanvasControl canvasViewer, string type, bool isControlLock) => canvasViewer.GetCanvasOverlayManager().SetLockControl(type, isControlLock);
		public static void SetLastGroupType(this ImageCanvasControl canvasViewer, string type) => canvasViewer.GetCanvasOverlayManager().LastGroupType = type;
		public static void SetVisible(this ImageCanvasControl canvasViewer, string type, bool visible)
		{
			canvasViewer.GetCanvasOverlayManager().SetVisible(type, visible);
			canvasViewer.InvalidateVisibleOverlayCache();
		}

		public static void SetAllVisible(this ImageCanvasControl canvasViewer, bool visible)
		{
			canvasViewer.GetCanvasOverlayManager().SetAllVisible(visible);
			canvasViewer.InvalidateVisibleOverlayCache();
		}
		public static CanvasOverlayItem GetLastGroup(this ImageCanvasControl canvasViewer) => canvasViewer.GetCanvasOverlayManager().GetGroupToType(canvasViewer.GetCanvasOverlayManager().LastGroupType);
		public static CanvasOverlayItem GetGroupToType(this ImageCanvasControl canvasViewer, string childType) => canvasViewer.GetCanvasOverlayManager().GetGroupToType(childType);
		public static CanvasOverlayItem GetOverlayByUniqueId(this ImageCanvasControl canvasViewer, string uniqueid) => canvasViewer.GetCanvasOverlayManager().GetOverlayByUniqueId(uniqueid);
		public static CanvasOverlayItem GetParentToType(this ImageCanvasControl canvasViewer, string childType) => canvasViewer.GetCanvasOverlayManager().GetParentToType(childType);
		public static string GetNewOverlayName(this ImageCanvasControl canvasViewer, CanvasOverlayItem overlayItem) => canvasViewer.GetCanvasOverlayManager().GetNewname(overlayItem);
		public static void ClearOverlays(this ImageCanvasControl canvasViewer)
		{
			ReleaseDisplayLists(canvasViewer);
			canvasViewer.GetCanvasOverlayManager().Clear();
			canvasViewer.InvalidateVisibleOverlayCache();
			canvasViewer.RefreshGL();
		}

		private static void ReleaseDisplayLists(ImageCanvasControl canvasViewer)
		{
			OpenGL gl = canvasViewer.GetOpenGL();
			foreach (CanvasOverlayItem overlayItem in canvasViewer.GetCanvasOverlayManager().GetAllOverlays())
			{
				ReleaseDisplayList(gl, overlayItem?.Shape);
			}
		}
		#endregion
	}
}
