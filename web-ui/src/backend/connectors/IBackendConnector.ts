export interface IBackendConnector {
    send(payload: any): Promise<boolean>;
}