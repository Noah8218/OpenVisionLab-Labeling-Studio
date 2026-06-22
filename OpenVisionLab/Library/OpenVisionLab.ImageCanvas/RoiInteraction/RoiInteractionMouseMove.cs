using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.Overlays;
using OpenVisionLab.ImageCanvas.OpenGLRendering;
using System;
using System.Drawing;

namespace OpenVisionLab.ImageCanvas
{
	public class RoiInteractionMouseMove
	{

		/// <summary>
		/// 湲몄씠 痢≪젙?????媛앹껜瑜??낅뜲?댄듃?⑸땲??
		/// </summary>
		/// <param name="viewModel"></param>
		/// <param name="measurement"></param>
		public static void UpdateMeasurement(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, ref Measurement measurement)
		{
			if (imageViewer.PreMousePos.IsEmpty) { return; }
			measurement = new Measurement()
			{
				StartPoint = imageViewer.PreMousePos,
				EndPoint = imageViewer.PostMousePos
			};
		}

		/// <summary>
		/// 留덉슦???쒕옒洹몃줈 Drawing??Rectangle???낅뜲?댄듃?⑸땲??
		/// </summary>
		/// <param name="viewModel"></param>
		/// <param name="activeRoiRect"></param>
		public static void UpdateReactangleToOverlay(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasRect<float> activeRoiRect)
		{
			if (imageViewer.PostMousePos.IsEmpty) { return; }
			if (imageViewer.PreMousePos.Equals(imageViewer.PostMousePos)) { return; }
			if (activeRoiRect == null) { return; }
			if (activeRoiRect.IsEditing == false) { return; }
			// ROI 怨꾩궛???⑸땲??
			RectangleF roi = new RectangleF(imageViewer.PreMousePos.X, imageViewer.PreMousePos.Y, imageViewer.PostMousePos.X - imageViewer.PreMousePos.X, imageViewer.PostMousePos.Y - imageViewer.PreMousePos.Y);
			activeRoiRect.UpdateRectangle(roi.Left, roi.Top, roi.Right, roi.Bottom);
		}

		/// <summary>
		/// Shape瑜?留덉슦???ъ씤?몃쭔???吏곸엯?덈떎.
		/// </summary>
		/// <param name="viewModel"></param>
		/// <param name="activeRoiRect"></param>
		/// <param name="currentRobotyPos"></param>
		/// <param name="imageSize"></param>
		public static void MoveRoiRect(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasRect<float> activeRoiRect, System.Drawing.PointF currentRobotyPos, System.Drawing.Size imageSize, bool canMoveRoi, OverlayEditingCompletedCallback callbackOverlayEditingComleted, bool notifyEditingCompleted = true)
		{
			if (activeRoiRect == null) { return; }
			var offsetX = currentRobotyPos.X - imageViewer.PreMousePos.X;
			var offsetY = currentRobotyPos.Y - imageViewer.PreMousePos.Y;

			if (offsetX != 0 || offsetY != 0)
			{
				CanvasSize<float> size = new CanvasSize<float>()
				{
					Width = (float)offsetX,
					Height = (float)offsetY
				};

				// During mouse drag, the caller can suppress GL display-list rebuilds and external UI callbacks.
				imageViewer.PerformRoiMove(activeRoiRect, size, imageSize, canMoveRoi, notifyEditingCompleted);
				UpdateRoiRect(imageViewer, activeRoiRect, notifyEditingCompleted ? callbackOverlayEditingComleted : null);
			}

			imageViewer.PreMousePos = currentRobotyPos;
		}

		public static void MoveOverlay(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasRect<float> activeRoiRect, System.Drawing.PointF currentRobotyPos, System.Drawing.Size imageSize, bool canMoveRoi, OverlayEditingCompletedCallback callbackOverlayEditingComleted, bool isGroup = false, bool notifyEditingCompleted = true)
		{
			try
			{
				if (activeRoiRect == null) return;

				var offsetX = currentRobotyPos.X - imageViewer.PreMousePos.X;
				var offsetY = currentRobotyPos.Y - imageViewer.PreMousePos.Y;

				if (offsetX == 0 && offsetY == 0) return;

				CanvasSize<float> size = new CanvasSize<float>() { Width = offsetX, Height = offsetY };

				// 怨듯넻 濡쒖쭅: ?대룞 泥섎━
				Action<CanvasRect<float>> moveAction = rect =>
				{
					// MouseMove should update geometry/index only; commit display-list/UI updates on mouse-up.
					imageViewer.PerformRoiMove(rect, size, imageSize, canMoveRoi, notifyEditingCompleted);
					UpdateRoiRect(imageViewer, rect, notifyEditingCompleted ? callbackOverlayEditingComleted : null);
				};

				if (isGroup)
				{
					// 洹몃９???랁븳 紐⑤뱺 ?ㅼ씠?닿렇???대룞
					CanvasOverlayItem currentGroup = imageViewer.GetGroupToType(activeRoiRect.GroupType);
					foreach (CanvasOverlayItem overlayItem in currentGroup.ChildObjects)
					{
						CanvasRect<float> rect = overlayItem.Shape as CanvasRect<float>;
						if (rect != null) moveAction(rect);
					}
				}
				else
				{
					// ?⑥씪 ?ㅼ씠?닿렇???대룞
					moveAction(activeRoiRect);
				}

				imageViewer.PreMousePos = currentRobotyPos;
			}
			catch
			{

			}

		}


		public static void MoveToGroupOverlay(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasRect<float> activeRoiRect, System.Drawing.PointF currentRobotyPos, System.Drawing.Size imageSize, bool canMoveRoi, OverlayEditingCompletedCallback callbackOverlayEditingComleted)
		{
			if (activeRoiRect == null) { return; }
			var offsetX = currentRobotyPos.X - imageViewer.PreMousePos.X;
			var offsetY = currentRobotyPos.Y - imageViewer.PreMousePos.Y;

			if (offsetX != 0 || offsetY != 0)
			{
				CanvasSize<float> size = new CanvasSize<float>()
				{
					Width = (float)offsetX,
					Height = (float)offsetY
				};

				CanvasOverlayItem currentGroup = imageViewer.GetGroupToType(activeRoiRect.GroupType);
				// ?대룞???? 洹몃９???랁븳 紐⑤뱺 ?ㅼ씠?닿렇?⑤뱾?먮룄 媛숈? 蹂?붾웾???곸슜?⑸땲??
				foreach (CanvasOverlayItem overlayItem in currentGroup.ChildObjects)
				{
					// 'shape'媛 'CanvasRect<float>' ??낆씤吏 ?뺤씤?⑸땲??
					CanvasRect<float> rect = overlayItem.Shape as CanvasRect<float>;
					if (rect != null)
					{
						imageViewer.PerformRoiMove(rect, size, imageSize, canMoveRoi); // ?댁쟾 OffsetMove 濡쒖쭅 ???PerformRoiMove ?ъ슜											
						UpdateRoiRect(imageViewer, rect, callbackOverlayEditingComleted);
					}
				}
			}
			imageViewer.PreMousePos = currentRobotyPos;
		}

		/// <summary>
		/// Shape size瑜?蹂寃쏀빀?덈떎.
		/// </summary>
		/// <param name="viewModel"></param>
		/// <param name="activeRoiRect"></param>
		/// <param name="currentRobotyPos"></param>
		/// <param name="imageSize"></param>
		public static void ResizeRoiRect(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasRect<float> activeRoiRect, System.Drawing.PointF currentRobotyPos, System.Drawing.Size imageSize, OverlayEditingCompletedCallback callbackOverlayEditingComleted, bool notifyEditingCompleted = true)
		{
			if (activeRoiRect == null) { return; }
			// Resize MouseMove updates only geometry; display-list rebuild happens once on mouse-up.
			activeRoiRect.Move(currentRobotyPos.X, currentRobotyPos.Y, imageSize, notifyEditingCompleted);
			UpdateRoiRect(imageViewer, activeRoiRect, notifyEditingCompleted ? callbackOverlayEditingComleted : null);
		}

		private static void UpdateRoiRect(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasRect<float> activeRoiRect, OverlayEditingCompletedCallback callbackOverlayEditingComleted)
		{
			if (activeRoiRect != null)
			{
				//_activeRoiRect = _targetOverlay;
				imageViewer.UpdateInteractiveOverlayIndex(activeRoiRect);
				activeRoiRect.IsChanged = true;
				callbackOverlayEditingComleted?.Invoke(activeRoiRect);
			}
		}
	}
}
