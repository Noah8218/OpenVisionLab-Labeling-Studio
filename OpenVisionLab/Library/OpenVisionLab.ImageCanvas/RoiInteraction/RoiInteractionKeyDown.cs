using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.Overlays;
using OpenVisionLab.ImageCanvas.OpenGLRendering;
using System;

namespace OpenVisionLab.ImageCanvas
{
	public class RoiInteractionKeyDown
	{
		/// <summary>
		/// ?꾩옱 ?좏깮???ш컖??(_activeRoiRect)??蹂듭궗?섎뒗 濡쒖쭅 援ы쁽			
		/// </summary>
		public static void CopyRectangle(CanvasRect<float> activeRoiRect, ref CanvasRect<float> copyRoiRect)
		{
			if (activeRoiRect != null) { copyRoiRect = activeRoiRect.DeepClone() as CanvasRect<float>; }
		}

		/// <summary>
		/// ?대┰蹂대뱶 ?먮뒗 ?꾩떆 蹂?섏뿉??蹂듭궗???ш컖?뺤쓣 媛?몄???遺숈뿬?ｋ뒗 濡쒖쭅 援ы쁽			
		/// </summary>
		public static void PasteRectangle(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, ref CanvasRect<float> copyRoiRect, OverlayAddedCallback callbackRoiAdded, OverlayGroupAddedCallback callbackGroupAddition)
		{
			if (imageViewer == null || copyRoiRect == null) return;

			CanvasOverlayItem overlayItem = imageViewer.GetOverlayByUniqueId(copyRoiRect.UniqueId);

			if (overlayItem?.IsGroupRectangle == true)
			{
				CopyGroupAddition(imageViewer, overlayItem, -60, 60, callbackGroupAddition);
			}
			else
			{
				// When the copied ROI is not in the current overlay manager, the operator
				// changed images. Paste at the same image coordinates for repeat labeling.
				bool pasteOnSameImage = overlayItem != null;
				CanvasRect<float> canvasRect = CreateOffsetRect(copyRoiRect, pasteOnSameImage ? 20 : 0, pasteOnSameImage ? 20 : 0);
				CanvasOverlayItem parentOverlay = ResolvePasteParentOverlay(imageViewer, copyRoiRect);
				if (parentOverlay == null)
				{
					return;
				}

				canvasRect.GroupType = parentOverlay.GroupType;
				AddNewOverlay(imageViewer, parentOverlay.GroupType, canvasRect.GroupType, canvasRect, canvasRect.UniqueId, parentOverlay.InspWindowType, EnumItemType.Window);

				// 肄쒕갚 ?몄텧
				callbackRoiAdded?.Invoke(canvasRect, parentOverlay);
				// Paste is a discrete object mutation, so repaint immediately instead of waiting
				// for the next mouse/viewport event to reveal the copied label.
				imageViewer.RefreshGL();
			}
		}

		private static CanvasOverlayItem ResolvePasteParentOverlay(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasRect<float> copyRoiRect)
		{
			return imageViewer.GetGroupToType(copyRoiRect.GroupType)
				?? imageViewer.GetLastGroup()
				?? imageViewer.GetGroupToType(EnumInspWindowType.Module.ToString());
		}

		private static void CopyGroupAddition(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, CanvasOverlayItem copiedGroupOverlay, float row, float col, OverlayGroupAddedCallback callbackGroupAddition)
		{

			CanvasOverlayItem parentOverlay = imageViewer.GetParentToType(copiedGroupOverlay.GroupType);
			string newGroupName = imageViewer.GetNewOverlayName(copiedGroupOverlay);

			CanvasRect<float> rect = CreateOffsetRect((CanvasRect<float>)copiedGroupOverlay.Shape, col, row * -1);
			AddNewOverlay(imageViewer, parentOverlay.GroupType, newGroupName, rect, rect.UniqueId, copiedGroupOverlay.InspWindowType, EnumItemType.Group);

			foreach (CanvasOverlayItem overlayItem in copiedGroupOverlay.ChildObjects)
			{
				CanvasRect<float> childRect = CreateOffsetRect((CanvasRect<float>)overlayItem.Shape, col, row * -1);
				AddNewOverlay(imageViewer, newGroupName, newGroupName, childRect, childRect.UniqueId, copiedGroupOverlay.InspWindowType, EnumItemType.Window);
			}


			OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs arg = new OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs();
			arg.Group = imageViewer.GetGroupToType(newGroupName);

			callbackGroupAddition?.Invoke(arg);
			imageViewer.RefreshGL();
		}

		private static CanvasRect<float> CreateOffsetRect(CanvasRect<float> sourceRect, float offsetX, float offsetY)
		{
			CanvasSize<float> offsetSize = new CanvasSize<float>()
			{
				Width = offsetX,
				Height = offsetY
			};

			CanvasRect<float> newRect = sourceRect.ToCanvasRect();
			newRect.GroupType = sourceRect.GroupType;
			newRect.UniqueId = Guid.NewGuid().ToString();
			newRect.OffsetMove(offsetSize, false);
			return newRect;
		}

		private static void AddNewOverlay(OpenVisionLab.ImageCanvas.Rendering.ImageCanvasControl imageViewer, string groupType, string groupNewName, CanvasRect<float> shape, string uniqueId, EnumInspWindowType inspWindowType, EnumItemType itemType, bool isExtensionRectangle = false)
		{
			bool isGroupRectangle = itemType == EnumItemType.Group ? true : false;
			imageViewer.AddOverlay(groupType, groupNewName, shape, uniqueId, inspWindowType, itemType, isExtensionRectangle, isGroupRectangle);
		}
	}
}
