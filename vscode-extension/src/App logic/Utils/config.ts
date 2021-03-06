import { ConfigurationTarget, Uri, workspace, WorkspaceConfiguration } from "vscode";

export class Config {
    static app: string = 'atddTestScriptor';

    public static getAddInitializeFunction(uri?: Uri): boolean {
        return this.getConfig(uri).get<boolean>('addInitializeFunction', true);
    }
    static setAddInitializeFunction(newValue: boolean | undefined, uri?: Uri) {
        this.getConfig(uri).update('addInitializeFunction', newValue, ConfigurationTarget.Workspace);
    }
    static getAddException(uri?: Uri): boolean {
        return this.getConfig(uri).get<boolean>('addException', false)
    }
    static getPrefixGiven(uri?: Uri): string {
        return this.getConfig(uri).get<string>('prefixGiven', '');
    }
    static getPrefixWhen(uri?: Uri): string {
        return this.getConfig(uri).get<string>('prefixWhen', '');
    }
    static getPrefixThen(uri?: Uri): string {
        return this.getConfig(uri).get<string>('prefixThen', '');
    }
    private static getConfig(uri?: Uri): WorkspaceConfiguration {
        return workspace.getConfiguration(this.app, uri);
    }
}