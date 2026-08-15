using System.Reflection;
using System.Windows.Input;
using SvgLiveEditor.Models;
using SvgLiveEditor.Services;
using SvgLiveEditor.ViewModels;

namespace SvgLiveEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowStructureSelectionRoutingIntegrationTests
{
    [TestMethod]
    [TestCategory("DesktopIntegration")]
    public void RealStructureRouteSynchronizesRangeAcrossBothTrees()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                RunOnSta();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.IsTrue(
            thread.Join(TimeSpan.FromSeconds(30)),
            "The Structure selection STA did not exit.");
        if (failure is not null)
        {
            throw new AssertFailedException(
                "The real Structure selection route failed.",
                failure);
        }
    }

    private static void RunOnSta()
    {
        string localApplicationData = Path.Combine(
            Path.GetTempPath(),
            "SvgLiveEditor.StructureSelection.Tests",
            Guid.NewGuid().ToString("N"));
        MainWindow? window = null;
        try
        {
            window = new MainWindow(localApplicationData);
            const string source =
                "<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"group\"><rect id=\"a\"/><circle id=\"b\"/><line id=\"c\"/></g></svg>";
            SvgDocumentIndex document =
                new SvgDocumentIndexService().Build(source).Document!;
            MainViewModel viewModel = GetField<MainViewModel>(
                window,
                "_viewModel");
            viewModel.Inspector.Load(
                document,
                preferredSelection: null,
                source: source);
            SvgElementViewModel group =
                viewModel.Inspector.Roots.Single().Children.Single();
            SvgElementViewModel first = group.Children[0];
            SvgElementViewModel second = group.Children[1];
            SvgElementViewModel third = group.Children[2];
            string editorBefore = window.SourceEditor.Text;

            InvokeStructureSelection(window, first, ModifierKeys.Control);
            InvokeStructureSelection(window, third, ModifierKeys.Shift);

            SvgMultiSelectionState range = GetField<SvgMultiSelectionState>(
                window,
                "_visualSelectionState");
            CollectionAssert.AreEqual(
                new[]
                {
                    first.Element.Identity,
                    second.Element.Identity,
                    third.Element.Identity
                },
                range.Identities.ToArray());
            Assert.AreEqual(third.Element.Identity, range.Primary);
            Assert.IsTrue(first.IsMultiSelected);
            Assert.IsTrue(second.IsMultiSelected);
            Assert.IsTrue(third.IsSelected);
            Assert.IsTrue(
                viewModel.Inspector.FindLayerViewModel(first.Element)!
                    .IsMultiSelected);

            InvokeStructureSelection(window, second, ModifierKeys.Control);
            SvgMultiSelectionState toggled = GetField<SvgMultiSelectionState>(
                window,
                "_visualSelectionState");
            CollectionAssert.AreEqual(
                new[] { first.Element.Identity, third.Element.Identity },
                toggled.Identities.ToArray());
            Assert.AreEqual(third.Element.Identity, toggled.Primary);
            Assert.AreEqual(editorBefore, window.SourceEditor.Text);
            Assert.IsFalse(window.SourceEditor.CanUndo);
        }
        finally
        {
            window?.Close();
            try
            {
                if (Directory.Exists(localApplicationData))
                {
                    Directory.Delete(localApplicationData, recursive: true);
                }
            }
            catch (IOException)
            {
                // The test owns this random directory; a delayed framework handle
                // is harmless and the OS temporary-directory policy will clean it.
            }
        }
    }

    private static void InvokeStructureSelection(
        MainWindow window,
        SvgElementViewModel element,
        ModifierKeys modifiers)
    {
        MethodInfo method = typeof(MainWindow).GetMethod(
            "HandleStructureMultiSelection",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                nameof(MainWindow),
                "HandleStructureMultiSelection");
        try
        {
            method.Invoke(window, [element, modifiers]);
        }
        catch (TargetInvocationException exception)
        {
            throw exception.InnerException ?? exception;
        }
    }

    private static T GetField<T>(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().Name, name);
        return (T)field.GetValue(instance)!;
    }
}
