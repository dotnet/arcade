export interface IServiceClient {
  readonly baseUri: string;
  fetch(input: RequestInfo, init?: RequestInit): Promise<Response>;
}

export class RestApiError<T> extends Error {
    private _res: Response;
    private _body: T;

    constructor(m: string, res: Response, body: T) {
        super(m);
        this._res = res;
        this._body = body;
    }

    get res(): Response {
        return this._res;
    }

    get body(): T {
        return this._body;
    }
}
