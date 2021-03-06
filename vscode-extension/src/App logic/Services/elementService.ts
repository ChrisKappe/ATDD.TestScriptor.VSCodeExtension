import { writeFileSync } from "fs-extra";
import { commands, Location, Position, Range, TextDocument, workspace, WorkspaceEdit } from "vscode";
import { Message, MessageUpdate, TypeChanged } from "../../typings/types";
import { ALFullSyntaxTreeNodeExt } from "../AL Code Outline Ext/alFullSyntaxTreeNodeExt";
import { FullSyntaxTreeNodeKind } from "../AL Code Outline Ext/fullSyntaxTreeNodeKind";
import { SyntaxTreeExt } from "../AL Code Outline Ext/syntaxTreeExt";
import { TextRangeExt } from "../AL Code Outline Ext/textRangeExt";
import { ALFullSyntaxTreeNode } from "../AL Code Outline/alFullSyntaxTreeNode";
import { SyntaxTree } from "../AL Code Outline/syntaxTree";
import { ElementUtils } from "../Utils/elementUtils";
import { RangeUtils } from "../Utils/rangeUtils";
import { TestCodeunitUtils } from "../Utils/testCodeunitUtils";
import { TestMethodUtils } from "../Utils/testMethodUtils";
import { ObjectService } from "./ObjectService";

export class ElementService {
    public static async addNewElementToCode(msg: MessageUpdate): Promise<boolean> {
        if (msg.Type == TypeChanged.Feature) {
            writeFileSync(msg.FsPath, TestCodeunitUtils.getDefaultTestCodeunit(msg.NewValue), { encoding: 'utf8' });
            return true;
        } else if (msg.Type == TypeChanged.ScenarioName) {
            //TODO: Get the workspacefolder using the msg.Project, search for the feature and add the scenario there.
            //msg.Id will contain the next ID of the Feature
            let fsPath: string = await ElementService.getFSPathOfFeature(msg.Project,msg.Feature);
            let document: TextDocument = await workspace.openTextDocument(fsPath);

            let syntaxTree: SyntaxTree = await SyntaxTree.getInstance(document);
            let methodTreeNodes: ALFullSyntaxTreeNode[] = syntaxTree.collectNodesOfKindXInWholeDocument(FullSyntaxTreeNodeKind.getMethodDeclaration());
            let testMethodTreeNodes = methodTreeNodes.filter(methodTreeNode => {
                let memberAttributes: ALFullSyntaxTreeNode[] = [];
                ALFullSyntaxTreeNodeExt.collectChildNodes(methodTreeNode, FullSyntaxTreeNodeKind.getMemberAttribute(), false, memberAttributes);
                let containsTestMemberAttribute: boolean = memberAttributes.some(memberAttribute => document.getText(TextRangeExt.createVSCodeRange(memberAttribute.fullSpan)).toLowerCase().includes('[test]'));
                return containsTestMemberAttribute;
            });
            testMethodTreeNodes = testMethodTreeNodes.sort((a, b) => a.fullSpan && a.fullSpan.end && b.fullSpan && b.fullSpan.end ? a.fullSpan?.end?.line - b.fullSpan?.end?.line : 0);
            let lastTestMethodTreeNode: ALFullSyntaxTreeNode = testMethodTreeNodes[testMethodTreeNodes.length - 1];
            let positionToInsert: Position = RangeUtils.trimRange(document, TextRangeExt.createVSCodeRange(lastTestMethodTreeNode.fullSpan)).end;
            let edit: WorkspaceEdit = new WorkspaceEdit();
            edit.insert(document.uri, positionToInsert, '\r\n\r\n' + TestCodeunitUtils.getDefaultTestMethod(msg.Feature, msg.Id, msg.NewValue, document.uri).join('\r\n'));
            let success = await workspace.applyEdit(edit);
            success = success && await document.save();
            return success;
        }
        else {
            let document: TextDocument = await workspace.openTextDocument(msg.FsPath);
            let scenarioRange: Range | undefined = ElementUtils.getRangeOfScenario(document, msg.Scenario);
            if (!scenarioRange)
                return false;

            let positionToInsert: Position | undefined = await ElementUtils.findPositionToInsertElement(document, scenarioRange, msg.Type);
            if (!positionToInsert)
                return false;

            let edit = new WorkspaceEdit();
            ElementUtils.addElement(edit, document, positionToInsert, msg.Type, msg.NewValue);
            let procedureName: string = TestMethodUtils.getProcedureName(msg.Type, msg.NewValue);
            ElementUtils.addProcedureCall(edit, document, positionToInsert, procedureName);
            await TestCodeunitUtils.addProcedure(edit, document, procedureName);

            let successful: boolean = await workspace.applyEdit(edit);
            await document.save();
            return successful;
        }
    }

    public static async getFSPathOfFeature(projectName: string, featureName: string): Promise<string> {
        let projects: any[] = await new ObjectService().getProjects();
        let project: any = projects.find(project => project.name == projectName);

        let basePath = (project.FilePath as string).substr(0, (project.FilePath as string).lastIndexOf('\\'));
        let objects: Message[] = await new ObjectService().getObjects([basePath]);
        let object: Message | undefined = objects.find(object => object.Feature == featureName);
        if (!object) {
            throw new Error('Feature ' + featureName + 'not found.');
        }
        return object.FsPath;
    }

    public static async modifyElementInCode(msg: MessageUpdate): Promise<boolean> {
        let document: TextDocument = await workspace.openTextDocument(msg.FsPath);
        let scenarioRange: Range | undefined = ElementUtils.getRangeOfScenario(document, msg.Scenario);
        if (!scenarioRange)
            return false;

        let syntaxTree: SyntaxTree = await SyntaxTree.getInstance(document);
        let methodTreeNode: ALFullSyntaxTreeNode | undefined = SyntaxTreeExt.getMethodOrTriggerTreeNodeOfCurrentPosition(syntaxTree, scenarioRange.start);
        if (!methodTreeNode)
            return false;
        let methodRange: Range = RangeUtils.trimRange(document, TextRangeExt.createVSCodeRange(methodTreeNode.fullSpan));

        let rangeOfOldElement: Range | undefined = ElementUtils.getRangeOfElementInsideMethodRange(document, methodRange, msg.Type, msg.OldValue);
        if (!rangeOfOldElement)
            throw new Error('Element to be renamed wasn\'t found.');

        let edit: WorkspaceEdit = new WorkspaceEdit();
        let newProcedureName = TestMethodUtils.getProcedureName(msg.Type, msg.NewValue);
        let oldProcedureName = TestMethodUtils.getProcedureName(msg.Type, msg.OldValue);
        if (await ElementUtils.existsProcedureCallToElementValue(document, methodRange.start, msg.Type, msg.OldValue)) {
            let identifierOfOldProcedureCall: ALFullSyntaxTreeNode = <ALFullSyntaxTreeNode>await ElementUtils.getProcedureCallToElementValue(document, rangeOfOldElement.start, msg.Type, msg.OldValue)
            let rangeOfOldIdentifier: Range = RangeUtils.trimRange(document, TextRangeExt.createVSCodeRange(identifierOfOldProcedureCall.fullSpan));
            let oldMethodTreeNode: ALFullSyntaxTreeNode | undefined = await SyntaxTreeExt.getMethodTreeNodeByCallPosition(document, rangeOfOldIdentifier.end);

            let alreadyRenamed: boolean = false;
            //add procedure similar to old one
            if (oldMethodTreeNode) {
                let parameterTypes: string[] = TestMethodUtils.getParameterTypesOfMethod(oldMethodTreeNode, document);
                if (!(await TestCodeunitUtils.isProcedureAlreadyDeclared(document, newProcedureName, parameterTypes))) {
                    let references: Location[] | undefined = await commands.executeCommand('vscode.executeReferenceProvider', document.uri, rangeOfOldIdentifier.end);
                    if (references && references.length == 2) {
                        await TestMethodUtils.renameMethod(edit, oldMethodTreeNode, document, newProcedureName);
                        alreadyRenamed = true;
                    } else {
                        let procedureHeaderOfOldMethod = TestMethodUtils.getProcedureHeaderOfMethod(oldMethodTreeNode, document);
                        let procedureHeaderOfNewMethod: string = procedureHeaderOfOldMethod.replace(new RegExp("procedure " + oldProcedureName, 'i'), 'procedure ' + newProcedureName);
                        await TestCodeunitUtils.addProcedureWithSpecificHeader(edit, document, newProcedureName, procedureHeaderOfNewMethod);
                    }
                }
            } else {
                //only happens if procedure call to old procedure exists, but no implementation to that one.
                if (!(await TestCodeunitUtils.isProcedureAlreadyDeclared(document, newProcedureName, [])))
                    TestCodeunitUtils.addProcedure(edit, document, newProcedureName);
            }
            //rename procedurecall and element
            ElementUtils.deleteElement(edit, document, rangeOfOldElement);
            if (!alreadyRenamed)
                ElementUtils.renameProcedureCall(edit, document, rangeOfOldIdentifier, newProcedureName);
            ElementUtils.addElement(edit, document, rangeOfOldElement.start.with({ character: 0 }), msg.Type, msg.NewValue);
        } else {
            ElementUtils.deleteElement(edit, document, rangeOfOldElement);
            ElementUtils.addProcedureCall(edit, document, rangeOfOldElement.start, newProcedureName);
            ElementUtils.addElement(edit, document, rangeOfOldElement.start, msg.Type, msg.NewValue);
            if (!(await TestCodeunitUtils.isProcedureAlreadyDeclared(document, newProcedureName, [])))
                TestCodeunitUtils.addProcedure(edit, document, newProcedureName);
        }

        let successful: boolean = await workspace.applyEdit(edit);
        await document.save();
        return successful;
    }

    public static async deleteElementFromCode(msg: MessageUpdate): Promise<boolean> {
        let edit: WorkspaceEdit = new WorkspaceEdit();
        let document: TextDocument = await workspace.openTextDocument(msg.FsPath);
        if ([TypeChanged.Given, TypeChanged.When, TypeChanged.Then].includes(msg.Type))
            await ElementUtils.deleteElementWithProcedureCall(edit, msg, document);
        else if (TypeChanged.ScenarioName == msg.Type && msg.ProceduresToDelete)
            await ElementUtils.deleteProcedures(edit, document, msg.ProceduresToDelete);

        let successful: boolean = await workspace.applyEdit(edit);
        await document.save();
        return successful;
    }
}